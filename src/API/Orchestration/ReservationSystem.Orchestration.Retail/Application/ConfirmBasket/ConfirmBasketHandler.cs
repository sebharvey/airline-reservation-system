using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using ReservationSystem.Shared.Common.Json;
using System.Linq;

namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

public sealed class ConfirmBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public ConfirmBasketHandler(
        OrderServiceClient orderServiceClient,
        OfferServiceClient offerServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public async Task<OrderResponse> HandleAsync(ConfirmBasketCommand command, CancellationToken cancellationToken)
    {
        // 1. Retrieve and validate basket
        var basket = await _orderServiceClient.GetBasketAsync(command.BasketId, cancellationToken)
            ?? throw new InvalidOperationException($"Basket {command.BasketId} not found.");

        if (basket.BasketStatus != "Active")
            throw new InvalidOperationException($"Basket is not active. Current status: {basket.BasketStatus}");

        if (basket.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Basket has expired.");

        var totalAmount = basket.TotalAmount ?? 0m;
        var currency = basket.CurrencyCode;
        var bookingType = command.LoyaltyPointsToRedeem.HasValue ? "Reward" : "Revenue";

        // 2. Create draft order in Order MS — no booking reference yet, basket remains active
        var draftOrder = await _orderServiceClient.CreateOrderAsync(
            command.BasketId,
            bookingType: bookingType,
            redemptionReference: null,
            cancellationToken);

        // 3. Initialise and authorise payment
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            paymentType: "Fare",
            method: command.PaymentMethod,
            currencyCode: currency,
            amount: totalAmount,
            description: $"Booking payment — basket {command.BasketId}",
            cancellationToken);

        try
        {
            await _paymentServiceClient.AuthoriseAsync(
                paymentId, totalAmount,
                command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName,
                cancellationToken);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "PaymentAuthorisationFailure", cancellationToken);
            throw;
        }

        // 4. Confirm order in Order MS — validates completeness, assigns booking reference,
        //    writes payment references into OrderData, and deletes the basket
        var paymentRefs = new List<object>
        {
            new { type = command.PaymentMethod, paymentReference = paymentId, amount = totalAmount }
        };

        OrderMsConfirmOrderResult confirmedOrder;
        try
        {
            confirmedOrder = await _orderServiceClient.ConfirmOrderAsync(
                draftOrder.OrderId,
                command.BasketId,
                paymentRefs,
                cancellationToken);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "OrderConfirmationFailure", cancellationToken);
            throw;
        }

        // 5. Sell flight inventory — convert held seats to sold in each cabin
        if (basket.BasketData.HasValue)
        {
            try
            {
                var (inventoryItems, paxCount) = ParseBasketDataForInventorySell(basket.BasketData.Value.GetRawText());
                if (inventoryItems.Count > 0 && paxCount > 0)
                {
                    await _offerServiceClient.SellInventoryAsync(
                        command.BasketId, inventoryItems, paxCount, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Inventory sell failure is logged but does not roll back the confirmed order —
                // the booking is already committed and the customer paid. Inventory can be
                // reconciled manually if needed.
                System.Console.Error.WriteLine(
                    $"[ConfirmBasket] Inventory sell failed for basket {command.BasketId}: {ex.Message}");
            }
        }

        // 6. Issue e-tickets via Delivery MS using the confirmed booking reference
        var issuedTickets = new List<IssuedTicket>();
        if (basket.BasketData.HasValue)
        {
            try
            {
                var (passengers, segments) = ParseBasketDataForTickets(basket.BasketData.Value.GetRawText());
                if (passengers.Count > 0 && segments.Count > 0)
                {
                    var formOfPayment = BuildFormOfPayment(command, paymentId, totalAmount, currency);
                    var passengersWithPayment = passengers
                        .Select(p => new TicketPassenger
                        {
                            PassengerId = p.PassengerId,
                            GivenName = p.GivenName,
                            Surname = p.Surname,
                            DateOfBirth = p.DateOfBirth,
                            FormOfPayment = formOfPayment
                        })
                        .ToList();

                    issuedTickets = await _deliveryServiceClient.IssueTicketsAsync(
                        command.BasketId,
                        confirmedOrder.BookingReference,
                        passengersWithPayment,
                        segments,
                        cancellationToken);

                    // 6. Write e-ticket numbers back into OrderData
                    if (issuedTickets.Count > 0)
                    {
                        var eTicketsJson = JsonSerializer.Serialize(
                            issuedTickets.Select(t => new { t.PassengerId, t.SegmentIds, t.ETicketNumber }),
                            SharedJsonOptions.CamelCase);
                        await _orderServiceClient.UpdateOrderETicketsAsync(
                            confirmedOrder.BookingReference, eTicketsJson, cancellationToken);

                        // 7. Populate departure manifest
                        var entries = BuildManifestEntries(issuedTickets, passengers, segments);
                        await _deliveryServiceClient.CreateManifestAsync(
                            confirmedOrder.BookingReference,
                            entries,
                            cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ticket/manifest failure after order confirmation — order is confirmed, tickets need manual issuance
                System.Console.Error.WriteLine($"[ConfirmBasket] Ticket issuance failed for {confirmedOrder.BookingReference}: {ex.Message}");
            }
        }

        // 8. Settle payment
        await _paymentServiceClient.SettleAsync(paymentId, totalAmount, cancellationToken);

        // 9. Link order to customer loyalty account (best-effort, non-blocking)
        try
        {
            var loyaltyNumber = basket.BasketData.HasValue
                ? ParseLoyaltyNumber(basket.BasketData.Value.GetRawText())
                : null;

            if (!string.IsNullOrWhiteSpace(loyaltyNumber))
            {
                await _customerServiceClient.LinkOrderToCustomerAsync(
                    loyaltyNumber, confirmedOrder.OrderId, confirmedOrder.BookingReference,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine(
                $"[ConfirmBasket] Customer order link failed for {confirmedOrder.BookingReference}: {ex.Message}");
        }

        return new OrderResponse
        {
            BookingReference = confirmedOrder.BookingReference,
            Status = confirmedOrder.OrderStatus,
            CustomerId = string.Empty,
            ETickets = issuedTickets.Select(t => new IssuedETicket
            {
                PassengerId = t.PassengerId,
                SegmentIds = t.SegmentIds,
                ETicketNumber = t.ETicketNumber
            }).ToList(),
            TotalPrice = confirmedOrder.TotalAmount ?? totalAmount,
            Currency = confirmedOrder.CurrencyCode,
            BookedAt = DateTime.UtcNow
        };
    }

    private static TicketFormOfPayment BuildFormOfPayment(
        ConfirmBasketCommand command, string paymentId, decimal amount, string currency)
    {
        string? maskedPan = null;
        string? cardType = null;
        string? expiryMmYy = null;

        if (!string.IsNullOrWhiteSpace(command.CardNumber))
        {
            var digits = new string(command.CardNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 10)
            {
                maskedPan = digits[..6] + new string('X', digits.Length - 10) + digits[^4..];
            }

            cardType = digits.FirstOrDefault() switch
            {
                '4' => "VI",
                '5' => "MC",
                '3' => "AX",
                '6' => "DC",
                _ => null
            };
        }

        if (!string.IsNullOrWhiteSpace(command.ExpiryDate))
        {
            var parts = command.ExpiryDate.Split('/');
            if (parts.Length == 2)
            {
                var month = parts[0].PadLeft(2, '0');
                var year = parts[1].Length == 4 ? parts[1][2..] : parts[1].PadLeft(2, '0');
                expiryMmYy = month + year;
            }
            else if (command.ExpiryDate.Length == 4 && command.ExpiryDate.All(char.IsDigit))
            {
                expiryMmYy = command.ExpiryDate;
            }
        }

        var type = command.PaymentMethod.ToUpperInvariant() switch
        {
            "CREDITCARD" or "CC" => "CC",
            "DEBITCARD" or "DC" => "DC",
            "CASH" => "CASH",
            _ => command.PaymentMethod.ToUpperInvariant()
        };

        return new TicketFormOfPayment
        {
            Type = type,
            CardType = cardType,
            MaskedPan = maskedPan,
            ExpiryMmYy = expiryMmYy,
            ApprovalCode = paymentId,
            Amount = amount,
            Currency = currency
        };
    }

    private static (List<TicketPassenger> passengers, List<TicketSegment> segments) ParseBasketDataForTickets(
        string basketDataJson)
    {
        var passengers = new List<TicketPassenger>();
        var segments = new List<TicketSegment>();

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passengersEl.EnumerateArray())
                {
                    passengers.Add(new TicketPassenger
                    {
                        PassengerId = p.TryGetProperty("passengerId", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        GivenName = p.TryGetProperty("givenName", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Surname = p.TryGetProperty("surname", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        DateOfBirth = p.TryGetProperty("dateOfBirth", out v) ? v.GetString() : null
                    });
                }
            }

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                var idx = 1;
                foreach (var offer in offersEl.EnumerateArray())
                {
                    var segmentId = offer.TryGetProperty("basketItemId", out var bid)
                        ? bid.GetString() ?? $"SEG-{idx}"
                        : $"SEG-{idx}";

                    segments.Add(new TicketSegment
                    {
                        SegmentId = segmentId,
                        InventoryId = offer.TryGetProperty("inventoryId", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        FlightNumber = offer.TryGetProperty("flightNumber", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        DepartureDate = offer.TryGetProperty("departureDate", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Origin = offer.TryGetProperty("origin", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Destination = offer.TryGetProperty("destination", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        CabinCode = offer.TryGetProperty("cabinCode", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        FareBasisCode = offer.TryGetProperty("fareBasisCode", out v) ? v.GetString() : null
                    });
                    idx++;
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return (passengers, segments);
    }

    private static List<ManifestEntry> BuildManifestEntries(
        List<IssuedTicket> tickets,
        List<TicketPassenger> passengers,
        List<TicketSegment> segments)
    {
        var passengerMap = passengers.ToDictionary(p => p.PassengerId);
        var segmentMap = segments.ToDictionary(s => s.SegmentId);
        var entries = new List<ManifestEntry>();

        foreach (var ticket in tickets)
        {
            passengerMap.TryGetValue(ticket.PassengerId, out var pax);
            foreach (var segmentId in ticket.SegmentIds)
            {
                segmentMap.TryGetValue(segmentId, out var seg);
                entries.Add(new ManifestEntry
                {
                    TicketId = ticket.TicketId,
                    InventoryId = seg?.InventoryId ?? string.Empty,
                    FlightNumber = seg?.FlightNumber ?? string.Empty,
                    DepartureDate = seg?.DepartureDate ?? string.Empty,
                    ETicketNumber = ticket.ETicketNumber,
                    PassengerId = ticket.PassengerId,
                    GivenName = pax?.GivenName ?? string.Empty,
                    Surname = pax?.Surname ?? string.Empty,
                    CabinCode = seg?.CabinCode ?? string.Empty
                });
            }
        }

        return entries;
    }

    private static string? ParseLoyaltyNumber(string basketDataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            if (doc.RootElement.TryGetProperty("loyaltyNumber", out var el) &&
                el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }
        }
        catch { /* Return null */ }
        return null;
    }

    private static (List<(Guid InventoryId, string CabinCode)> items, int paxCount) ParseBasketDataForInventorySell(
        string basketDataJson)
    {
        var items = new List<(Guid, string)>();
        var paxCount = 0;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                paxCount = passengersEl.GetArrayLength();
            }

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offersEl.EnumerateArray())
                {
                    if (offer.TryGetProperty("inventoryId", out var invIdEl) &&
                        invIdEl.TryGetGuid(out var inventoryId) &&
                        offer.TryGetProperty("cabinCode", out var cabinEl))
                    {
                        var cabinCode = cabinEl.GetString() ?? "Y";
                        items.Add((inventoryId, cabinCode));
                    }
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return (items, paxCount);
    }
}
