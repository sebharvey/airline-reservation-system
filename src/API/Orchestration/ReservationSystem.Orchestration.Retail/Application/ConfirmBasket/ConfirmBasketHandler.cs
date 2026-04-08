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

        // 5–8. Run post-confirm operations in parallel:
        //   - Hold + sell inventory (holds run in parallel across segments, then sell)
        //   - Issue e-tickets + write back to order
        //   - Settle payment
        //   - Link order to customer loyalty account
        var basketDataJson = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;

        var inventoryTask = RunInventorySellAsync(basketDataJson, draftOrder.OrderId, command.BasketId, cancellationToken);
        var ticketsTask   = RunTicketIssuanceAsync(basketDataJson, command, paymentId, totalAmount, currency, confirmedOrder, cancellationToken);
        var settleTask    = _paymentServiceClient.SettleAsync(paymentId, totalAmount, cancellationToken);
        var customerTask  = RunCustomerLinkAsync(basketDataJson, confirmedOrder, cancellationToken);

        await Task.WhenAll(inventoryTask, ticketsTask, settleTask, customerTask);

        var issuedTickets = await ticketsTask;

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

    private async Task RunInventorySellAsync(
        string? basketDataJson, Guid orderId, Guid basketId, CancellationToken cancellationToken)
    {
        if (basketDataJson == null) return;
        try
        {
            var (inventoryItems, passengerIds, seatsByInventory) = ParseBasketDataForInventorySell(basketDataJson);
            if (inventoryItems.Count == 0 || passengerIds.Count == 0) return;

            // Hold all inventory segments in parallel
            await Task.WhenAll(inventoryItems.Select(item =>
            {
                var (inventoryId, cabinCode) = item;
                List<(string? SeatNumber, string? PassengerId)> passengers;
                if (seatsByInventory.TryGetValue(inventoryId.ToString(), out var seats) && seats.Count > 0)
                    passengers = seats;
                else
                    passengers = passengerIds.Select(id => ((string?)null, (string?)id)).ToList();

                return _offerServiceClient.HoldInventoryAsync(inventoryId, cabinCode, passengers, orderId, cancellationToken);
            }));

            await _offerServiceClient.SellInventoryAsync(orderId, inventoryItems, cancellationToken);
        }
        catch (Exception ex)
        {
            // Inventory sell failure is logged but does not roll back the confirmed order —
            // the booking is already committed and the customer paid. Inventory can be
            // reconciled manually if needed.
            System.Console.Error.WriteLine(
                $"[ConfirmBasket] Inventory sell failed for basket {basketId}: {ex.Message}");
        }
    }

    private async Task<List<IssuedTicket>> RunTicketIssuanceAsync(
        string? basketDataJson,
        ConfirmBasketCommand command,
        string paymentId,
        decimal totalAmount,
        string currency,
        OrderMsConfirmOrderResult confirmedOrder,
        CancellationToken cancellationToken)
    {
        if (basketDataJson == null) return [];
        try
        {
            var (passengers, segments) = ParseBasketDataForTickets(basketDataJson);
            if (passengers.Count == 0 || segments.Count == 0) return [];

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

            var issuedTickets = await _deliveryServiceClient.IssueTicketsAsync(
                command.BasketId,
                confirmedOrder.BookingReference,
                passengersWithPayment,
                segments,
                cancellationToken);

            if (issuedTickets.Count > 0)
            {
                var eTicketsJson = JsonSerializer.Serialize(
                    issuedTickets.Select(t => new { t.PassengerId, t.SegmentIds, t.ETicketNumber }),
                    SharedJsonOptions.CamelCase);
                await _orderServiceClient.UpdateOrderETicketsAsync(
                    confirmedOrder.BookingReference, eTicketsJson, cancellationToken);
            }

            return issuedTickets;
        }
        catch (Exception ex)
        {
            // Ticket issuance failure after order confirmation — order is confirmed, tickets need manual issuance
            System.Console.Error.WriteLine($"[ConfirmBasket] Ticket issuance failed for {confirmedOrder.BookingReference}: {ex.Message}");
            return [];
        }
    }

    private async Task RunCustomerLinkAsync(
        string? basketDataJson, OrderMsConfirmOrderResult confirmedOrder, CancellationToken cancellationToken)
    {
        try
        {
            var loyaltyNumber = basketDataJson != null ? ParseLoyaltyNumber(basketDataJson) : null;
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

            // Build a lookup of seat assignments keyed by inventoryId (which is how the
            // web app stores the segmentId on each seat selection in the basket).
            var seatsByInventoryId = new Dictionary<string, List<SeatAssignmentItem>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var paxId = seat.TryGetProperty("passengerId", out var spid) ? spid.GetString() ?? "" : "";
                    var segId = seat.TryGetProperty("segmentId", out var ssid) ? ssid.GetString() ?? "" : "";
                    var seatNum = seat.TryGetProperty("seatNumber", out var sn) ? sn.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId) && !string.IsNullOrEmpty(seatNum))
                    {
                        if (!seatsByInventoryId.ContainsKey(segId))
                            seatsByInventoryId[segId] = [];
                        seatsByInventoryId[segId].Add(new SeatAssignmentItem { PassengerId = paxId, SeatNumber = seatNum });
                    }
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

                    var inventoryId = offer.TryGetProperty("inventoryId", out var v) ? v.GetString() ?? string.Empty : string.Empty;

                    segments.Add(new TicketSegment
                    {
                        SegmentId = segmentId,
                        InventoryId = inventoryId,
                        FlightNumber = offer.TryGetProperty("flightNumber", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        DepartureDate = offer.TryGetProperty("departureDate", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Origin = offer.TryGetProperty("origin", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Destination = offer.TryGetProperty("destination", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        CabinCode = offer.TryGetProperty("cabinCode", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        FareBasisCode = offer.TryGetProperty("fareBasisCode", out v) ? v.GetString() : null,
                        SeatAssignments = seatsByInventoryId.TryGetValue(inventoryId, out var segSeats) ? segSeats : []
                    });
                    idx++;
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return (passengers, segments);
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

    private static (
        List<(Guid InventoryId, string CabinCode)> items,
        List<string> passengerIds,
        Dictionary<string, List<(string? SeatNumber, string? PassengerId)>> seatsByInventory)
        ParseBasketDataForInventorySell(string basketDataJson)
    {
        var items = new List<(Guid, string)>();
        var passengerIds = new List<string>();
        var seatsByInventory = new Dictionary<string, List<(string? SeatNumber, string? PassengerId)>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passengersEl.EnumerateArray())
                {
                    var paxId = p.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
                    if (!string.IsNullOrEmpty(paxId))
                        passengerIds.Add(paxId);
                }
            }

            // seats[].segmentId is the inventoryId — build per-flight seat lists with passenger IDs.
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var segId   = seat.TryGetProperty("segmentId",   out var sid)  ? sid.GetString()  : null;
                    var seatNum = seat.TryGetProperty("seatNumber",  out var sn)   ? sn.GetString()   : null;
                    var paxId   = seat.TryGetProperty("passengerId", out var pid)  ? pid.GetString()  : null;
                    if (!string.IsNullOrEmpty(segId))
                    {
                        if (!seatsByInventory.ContainsKey(segId))
                            seatsByInventory[segId] = [];
                        seatsByInventory[segId].Add((string.IsNullOrEmpty(seatNum) ? null : seatNum, paxId));
                    }
                }
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

        return (items, passengerIds, seatsByInventory);
    }
}
