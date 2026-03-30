using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Linq;

namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

public sealed class ConfirmBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public ConfirmBasketHandler(
        OrderServiceClient orderServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
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

        // 2. Initialise and authorise payment
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

        // 3. Create order in Order MS — booking reference generated here
        var paymentRefs = new List<object>
        {
            new { type = command.PaymentMethod, paymentReference = paymentId, amount = totalAmount }
        };

        var bookingType = command.LoyaltyPointsToRedeem.HasValue ? "Reward" : "Revenue";

        OrderMsCreateOrderResult order;
        try
        {
            order = await _orderServiceClient.CreateOrderAsync(
                command.BasketId,
                eTickets: [],
                paymentReferences: paymentRefs,
                bookingType: bookingType,
                redemptionReference: null,
                cancellationToken);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "OrderCreationFailure", cancellationToken);
            throw;
        }

        // 4. Issue e-tickets via Delivery MS using the confirmed booking reference
        var issuedTickets = new List<IssuedTicket>();
        if (basket.BasketData.HasValue && !string.IsNullOrEmpty(order.BookingReference))
        {
            try
            {
                var (passengers, segments) = ParseBasketDataForTickets(basket.BasketData.Value.GetRawText());
                if (passengers.Count > 0 && segments.Count > 0)
                {
                    issuedTickets = await _deliveryServiceClient.IssueTicketsAsync(
                        command.BasketId,
                        order.BookingReference,
                        passengers,
                        segments,
                        cancellationToken);

                    // 5. Write e-ticket numbers back into OrderData
                    if (issuedTickets.Count > 0)
                    {
                        var eTicketsJson = System.Text.Json.JsonSerializer.Serialize(
                            issuedTickets.Select(t => new { t.PassengerId, t.SegmentId, t.ETicketNumber }));
                        await _orderServiceClient.UpdateOrderETicketsAsync(
                            order.BookingReference, eTicketsJson, cancellationToken);

                        // 6. Populate departure manifest
                        var entries = BuildManifestEntries(issuedTickets, passengers, segments);
                        await _deliveryServiceClient.CreateManifestAsync(
                            order.BookingReference,
                            entries,
                            cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ticket/manifest failure after order creation — order is confirmed, tickets need manual issuance
                System.Console.Error.WriteLine($"[ConfirmBasket] Ticket issuance failed for {order.BookingReference}: {ex.Message}");
            }
        }

        // 7. Settle payment
        await _paymentServiceClient.SettleAsync(paymentId, totalAmount, cancellationToken);

        return new OrderResponse
        {
            BookingReference = order.BookingReference ?? string.Empty,
            Status = order.OrderStatus,
            CustomerId = string.Empty,
            ETickets = issuedTickets.Select(t => new IssuedETicket
            {
                PassengerId = t.PassengerId,
                SegmentId = t.SegmentId,
                ETicketNumber = t.ETicketNumber
            }).ToList(),
            TotalPrice = order.TotalAmount ?? totalAmount,
            Currency = order.CurrencyCode,
            BookedAt = DateTime.UtcNow
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
            segmentMap.TryGetValue(ticket.SegmentId, out var seg);

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

        return entries;
    }
}
