using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.CancelOrder;

/// <summary>
/// Orchestrates voluntary booking cancellation across Delivery, Offer, Customer and Order microservices.
///
/// Sequence:
/// 1. Retrieve order — collect fare conditions, e-tickets, inventory IDs, booking type.
/// 2. Void each e-ticket via Delivery MS.
/// 3. Release inventory via Offer MS per segment.
/// 4. (Reward) Reinstate points via Customer MS.
/// 5. Cancel order in Order MS — publishes OrderCancelled event with refundableAmount.
/// </summary>
public sealed class CancelOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public CancelOrderHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        OfferServiceClient offerServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _offerServiceClient = offerServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public async Task<CancelOrderResponse> HandleAsync(string bookingReference, CancellationToken ct)
    {
        // 1. Retrieve order
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, ct)
            ?? throw new KeyNotFoundException($"Order '{bookingReference}' not found.");

        if (order.OrderStatus == "Cancelled")
            throw new InvalidOperationException($"Order '{bookingReference}' is already cancelled.");

        if (order.OrderStatus != "Confirmed" && order.OrderStatus != "Changed")
            throw new InvalidOperationException($"Order cannot be cancelled. Status: {order.OrderStatus}");

        // Parse order data
        var orderData = order.OrderData ?? default;
        var eTickets = ExtractETickets(orderData);
        var bookingType = ExtractBookingType(orderData);
        var (totalPaid, cancellationFee, isRefundable) = ExtractFareConditions(orderData);
        var loyaltyNumber = ExtractLoyaltyNumber(orderData);
        var totalPointsAmount = ExtractTotalPoints(orderData);

        // 2. Void each e-ticket
        foreach (var eTicket in eTickets)
        {
            try { await _deliveryServiceClient.VoidTicketAsync(eTicket, ct); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CancelOrder] Ticket void failed for {eTicket}: {ex.Message}");
            }
        }

        // 3. Release inventory per segment
        foreach (var (inventoryId, cabinCode) in ExtractInventoryItems(orderData))
        {
            try
            {
                if (Guid.TryParse(inventoryId, out var invGuid))
                    await _offerServiceClient.ReleaseInventoryAsync(invGuid, cabinCode, order.OrderId, "Sold", ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CancelOrder] Inventory release failed for {inventoryId}: {ex.Message}");
            }
        }

        // 4. (Reward) Reinstate points
        int? pointsReinstated = null;
        if (bookingType == "Reward" && !string.IsNullOrEmpty(loyaltyNumber) && totalPointsAmount > 0)
        {
            try
            {
                await _customerServiceClient.ReinstatePointsAsync(
                    loyaltyNumber, totalPointsAmount, "VoluntaryCancellation", ct);
                pointsReinstated = totalPointsAmount;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CancelOrder] Points reinstatement failed: {ex.Message}");
            }
        }

        // 5. Cancel order in Order MS
        var refundableAmount = isRefundable ? Math.Max(0m, totalPaid - cancellationFee) : 0m;
        await _orderServiceClient.CancelOrderAsync(bookingReference, new
        {
            refundableAmount,
            cancellationFeeAmount = cancellationFee,
            originalTotalPaid = totalPaid,
            reason = "VoluntaryCancellation"
        }, ct);

        return new CancelOrderResponse
        {
            BookingReference = bookingReference,
            OrderStatus = "Cancelled",
            RefundableAmount = refundableAmount,
            CancellationFeeAmount = cancellationFee,
            RefundInitiated = refundableAmount > 0,
            PointsReinstated = pointsReinstated
        };
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

    private static string ExtractLoyaltyNumber(JsonElement data)
    {
        if (!data.Equals(default) &&
            data.TryGetProperty("pointsRedemption", out var pr) &&
            pr.TryGetProperty("loyaltyNumber", out var ln))
            return ln.GetString() ?? "";
        return "";
    }

    private static int ExtractTotalPoints(JsonElement data)
    {
        if (!data.Equals(default) &&
            data.TryGetProperty("pointsRedemption", out var pr) &&
            pr.TryGetProperty("pointsRedeemed", out var pts))
            return pts.GetInt32();
        return 0;
    }

    private static (decimal TotalPaid, decimal CancellationFee, bool IsRefundable) ExtractFareConditions(JsonElement data)
    {
        decimal totalPaid = 0m;
        decimal cancellationFee = 0m;
        var isRefundable = false;

        if (data.Equals(default)) return (totalPaid, cancellationFee, isRefundable);

        if (data.TryGetProperty("orderItems", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("totalAmount", out var ta))
                    totalPaid += ta.GetDecimal();
                if (item.TryGetProperty("isRefundable", out var ir) && ir.GetBoolean())
                    isRefundable = true;
            }
        }

        // Cancellation fee from the first refundable item's conditions (simplified: zero fee by default)
        cancellationFee = 0m;

        return (totalPaid, cancellationFee, isRefundable);
    }
}

public sealed class CancelOrderResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public decimal RefundableAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public bool RefundInitiated { get; init; }
    public int? PointsReinstated { get; init; }
}
