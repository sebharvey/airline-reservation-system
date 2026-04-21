using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.ChangeOrder;

/// <summary>
/// Orchestrates voluntary flight change across Offer, Payment, Delivery, Customer and Order microservices.
///
/// Sequence (revenue booking with add-collect):
/// 1. Retrieve order — confirm isChangeable, collect changeFee, originalBaseFare, originalPaymentId.
/// 2. Validate new offer from Offer MS.
/// 3. (Revenue, addCollect > 0) Authorise change payment via Payment MS.
/// 4. Void original e-tickets via Delivery MS.
/// 5. Release original inventory via Offer MS.
/// 6. Update Order MS with new segment data (status → Changed).
/// 7. Reissue e-tickets via Delivery MS (reason=VoluntaryChange).
/// 8. (Revenue, addCollect > 0) Settle change payment.
/// </summary>
public sealed class ChangeOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public ChangeOrderHandler(
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

    public async Task<ChangeOrderResponse> HandleAsync(
        string bookingReference,
        ChangeOrderCommand command,
        CancellationToken ct)
    {
        // 1. Retrieve and validate order
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, ct)
            ?? throw new KeyNotFoundException($"Order '{bookingReference}' not found.");

        if (order.OrderStatus != "Confirmed" && order.OrderStatus != "Changed")
            throw new InvalidOperationException($"Order is not mutable. Status: {order.OrderStatus}");

        var orderData = order.OrderData ?? default;
        var bookingType = ExtractBookingType(orderData);

        // Check fare allows change
        var isChangeable = IsChangeable(orderData);
        if (!isChangeable)
            throw new InvalidOperationException("Fare conditions do not permit voluntary change.");

        var originalBaseFare = ExtractOriginalBaseFare(orderData);
        var currency = order.CurrencyCode;
        var passengers = ExtractPassengers(orderData);
        var paxCount = passengers.Count == 0 ? 1 : passengers.Count;

        // 2. Validate new offer from Offer MS
        if (!Guid.TryParse(command.NewOfferId, out var newOfferId))
            throw new InvalidOperationException($"Invalid offerId: '{command.NewOfferId}'.");

        var newOffer = await _offerServiceClient.GetOfferAsync(newOfferId, cancellationToken: ct)
            ?? throw new InvalidOperationException($"New offer '{command.NewOfferId}' not found or has expired.");

        var newOfferItem = newOffer.Offers.FirstOrDefault();
        var newBaseFare = newOfferItem?.BaseFareAmount ?? 0m;
        var addCollect = Math.Max(0m, newBaseFare - originalBaseFare);
        var totalDue = addCollect; // changeFee is zero in this simplified implementation

        // 3. (Revenue, totalDue > 0) Authorise change payment
        string? changePaymentId = null;
        if (bookingType == "Revenue" && totalDue > 0)
        {
            if (command.Payment is null)
                throw new InvalidOperationException("Payment details are required for this flight change.");

            changePaymentId = await _paymentServiceClient.InitialiseAsync(
                command.Payment.Method,
                currency,
                totalDue,
                $"Flight change — {bookingReference}",
                ct);

            try
            {
                await _paymentServiceClient.AuthoriseAsync(
                    changePaymentId,
                    "FareChange",
                    totalDue,
                    command.Payment.CardNumber,
                    command.Payment.ExpiryDate,
                    command.Payment.Cvv,
                    command.Payment.CardholderName,
                    ct);
            }
            catch
            {
                await _paymentServiceClient.VoidAsync(changePaymentId, "PaymentAuthorisationFailure", ct);
                throw;
            }
        }

        // 4. Void original e-tickets
        var eTickets = ExtractETickets(orderData);
        foreach (var eTicket in eTickets)
        {
            try { await _deliveryServiceClient.VoidTicketAsync(eTicket, ct); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ChangeOrder] Ticket void failed for {eTicket}: {ex.Message}");
            }
        }

        // 5. Release original inventory
        foreach (var (inventoryId, cabinCode) in ExtractInventoryItems(orderData))
        {
            try
            {
                if (Guid.TryParse(inventoryId, out var invGuid))
                    await _offerServiceClient.ReleaseInventoryAsync(invGuid, cabinCode, order.OrderId, "Sold", ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ChangeOrder] Inventory release failed: {ex.Message}");
            }
        }

        var newCabinCode = newOfferItem?.CabinCode ?? "Y";
        var newFlightNum = newOffer.FlightNumber;
        var newDepDateStr = newOffer.DepartureDate;

        // 7. Update Order MS with new segment
        var changeData = new
        {
            newOfferId = command.NewOfferId,
            flightNumber = newFlightNum,
            departureDate = newDepDateStr,
            departureTime = newOffer.DepartureTime,
            arrivalTime = newOffer.ArrivalTime,
            origin = newOffer.Origin,
            destination = newOffer.Destination,
            aircraftType = newOffer.AircraftType,
            cabinCode = newCabinCode,
            inventoryId = newOffer.InventoryId.ToString(),
            baseFareAmount = newOfferItem?.BaseFareAmount ?? 0m,
            taxAmount = newOfferItem?.TaxAmount ?? 0m,
            totalAmount = newOfferItem?.TotalAmount ?? 0m,
            isRefundable = newOfferItem?.IsRefundable ?? false,
            isChangeable = newOfferItem?.IsChangeable ?? false,
            addCollect = totalDue,
            changePaymentId
        };

        await _orderServiceClient.ChangeOrderAsync(bookingReference, changeData, ct);

        // 7. Reissue e-tickets
        List<IssuedTicket> newTickets = [];
        if (passengers.Count > 0)
        {
            try
            {
                var ticketPassengers = passengers.Select(p => new TicketPassenger
                {
                    PassengerId = p.PassengerId,
                    GivenName = p.GivenName,
                    Surname = p.Surname
                }).ToList();

                var ticketSegments = new List<TicketSegment>
                {
                    new TicketSegment
                    {
                        SegmentId = newOffer.InventoryId.ToString(),
                        InventoryId = newOffer.InventoryId.ToString(),
                        FlightNumber = newFlightNum,
                        DepartureDate = newDepDateStr,
                        Origin = newOffer.Origin,
                        Destination = newOffer.Destination,
                        CabinCode = newCabinCode
                    }
                };

                newTickets = await _deliveryServiceClient.ReissueTicketsAsync(
                    bookingReference, "VoluntaryChange", ticketPassengers, ticketSegments, ct);

                // Write new e-ticket numbers back to order
                if (newTickets.Count > 0)
                {
                    var eTicketsJson = System.Text.Json.JsonSerializer.Serialize(
                        newTickets.Select(t => new { t.PassengerId, t.SegmentIds, t.ETicketNumber }));
                    await _orderServiceClient.UpdateOrderETicketsAsync(bookingReference, eTicketsJson, ct);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ChangeOrder] Ticket reissuance failed: {ex.Message}");
            }
        }

        // 8. Settle change payment
        if (changePaymentId is not null)
        {
            await _paymentServiceClient.SettleAsync(changePaymentId, totalDue, ct);
        }

        return new ChangeOrderResponse
        {
            BookingReference = bookingReference,
            NewFlightNumber = newFlightNum,
            NewDepartureDate = newDepDateStr,
            TotalDue = totalDue,
            PaymentId = changePaymentId,
            NewETicketNumbers = newTickets.Select(t => t.ETicketNumber).ToList()
        };
    }

    private static bool IsChangeable(JsonElement data)
    {
        if (data.Equals(default)) return false;
        if (data.TryGetProperty("orderItems", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("isChangeable", out var ic) && ic.GetBoolean())
                    return true;
            }
        }
        return false;
    }

    private static decimal ExtractOriginalBaseFare(JsonElement data)
    {
        if (data.Equals(default)) return 0m;
        if (data.TryGetProperty("orderItems", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("baseFareAmount", out var bf))
                    return bf.GetDecimal();
            }
        }
        return 0m;
    }

    private static List<string> ExtractETickets(JsonElement data)
    {
        var result = new List<string>();
        if (!data.Equals(default) && data.TryGetProperty("eTickets", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var et in arr.EnumerateArray())
            {
                if (et.TryGetProperty("eTicketNumber", out var n) && n.GetString() is { } etNum)
                    result.Add(etNum);
            }
        }
        return result;
    }

    private static List<(string InventoryId, string CabinCode)> ExtractInventoryItems(JsonElement data)
    {
        var result = new List<(string, string)>();
        if (!data.Equals(default) && data.TryGetProperty("orderItems", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var inv = item.TryGetProperty("inventoryId", out var i) ? i.GetString() ?? "" : "";
                var cabin = item.TryGetProperty("cabinCode", out var c) ? c.GetString() ?? "Y" : "Y";
                if (!string.IsNullOrEmpty(inv))
                    result.Add((inv, cabin));
            }
        }
        return result;
    }

    private static string ExtractBookingType(JsonElement data)
    {
        if (!data.Equals(default) && data.TryGetProperty("bookingType", out var bt))
            return bt.GetString() ?? "Revenue";
        return "Revenue";
    }

    private static List<(string PassengerId, string GivenName, string Surname)> ExtractPassengers(JsonElement data)
    {
        var result = new List<(string, string, string)>();
        if (!data.Equals(default) &&
            data.TryGetProperty("dataLists", out var dl) &&
            dl.TryGetProperty("passengers", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in arr.EnumerateArray())
            {
                var pid = p.TryGetProperty("passengerId", out var id) ? id.GetString() ?? "" : "";
                var gn = p.TryGetProperty("givenName", out var g) ? g.GetString() ?? "" : "";
                var sn = p.TryGetProperty("surname", out var s) ? s.GetString() ?? "" : "";
                result.Add((pid, gn, sn));
            }
        }
        return result;
    }
}

public sealed class ChangeOrderCommand
{
    public string NewOfferId { get; init; } = string.Empty;
    public PaymentDetailsForChange? Payment { get; init; }
}

public sealed class PaymentDetailsForChange
{
    public string Method { get; init; } = "CreditCard";
    public string CardNumber { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
    public string CardholderName { get; init; } = string.Empty;
}

public sealed class ChangeOrderResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string NewFlightNumber { get; init; } = string.Empty;
    public string NewDepartureDate { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }
    public string? PaymentId { get; init; }
    public List<string> NewETicketNumbers { get; init; } = [];
}
