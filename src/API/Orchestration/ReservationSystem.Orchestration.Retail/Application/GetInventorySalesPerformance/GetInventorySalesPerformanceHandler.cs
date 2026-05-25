using System.Globalization;
using System.Text.Json.Nodes;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.GetInventorySalesPerformance;

public sealed class GetInventorySalesPerformanceHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;

    public GetInventorySalesPerformanceHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SalesPerformanceResponse> HandleAsync(
        GetInventorySalesPerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var inventoryIdStr = query.InventoryId.ToString();

        var holds = await _offerServiceClient.GetInventoryHoldsAsync(query.InventoryId, cancellationToken);
        var revenueHolds = holds.Where(h => h.HoldType == "Revenue").ToList();

        var orderIds = revenueHolds.Select(h => h.OrderId).Distinct().ToList();
        var bookingRefs = await _orderServiceClient.GetBookingReferencesAsync(orderIds, cancellationToken);

        var orderTasks = bookingRefs
            .Where(kv => kv.Value != null)
            .Select(async kv => (kv.Key, await _orderServiceClient.GetOrderByRefAsync(kv.Value!, cancellationToken)));
        var orders = (await Task.WhenAll(orderTasks))
            .Where(r => r.Item2 != null)
            .ToDictionary(r => r.Key, r => r.Item2!);

        var currency = orders.Values.FirstOrDefault()?.CurrencyCode ?? "GBP";

        // Build a zero-filled bucket for each day in the window [today-days+1 .. today].
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-query.Days + 1);
        var dayMap = new Dictionary<string, (int orderCount, int passengerCount, decimal revenue, decimal ancillary)>();
        for (var d = startDate; d <= today; d = d.AddDays(1))
            dayMap[d.ToString("yyyy-MM-dd")] = (0, 0, 0m, 0m);

        // Group holds by UTC booking date, then aggregate per day.
        var holdsByDate = revenueHolds
            .Select(h =>
            {
                var ok = DateTimeOffset.TryParse(
                    h.CreatedAt, null, DateTimeStyles.AssumeUniversal, out var dt);
                return (Hold: h, Ok: ok, Date: ok ? dt.UtcDateTime.Date.ToString("yyyy-MM-dd") : null);
            })
            .Where(x => x.Ok && x.Date != null && dayMap.ContainsKey(x.Date!))
            .GroupBy(x => x.Date!);

        foreach (var dateGroup in holdsByDate)
        {
            var dateKey = dateGroup.Key;
            var (ordCnt, paxCnt, rev, anc) = dayMap[dateKey];

            var uniqueOrderIds = dateGroup.Select(x => x.Hold.OrderId).Distinct().ToList();
            ordCnt += uniqueOrderIds.Count;
            paxCnt += dateGroup.Sum(x => x.Hold.PaxCount);

            foreach (var orderId in uniqueOrderIds)
            {
                if (!orders.TryGetValue(orderId, out var order) || !order.OrderData.HasValue) continue;
                var orderData = JsonNode.Parse(order.OrderData.Value.GetRawText())?.AsObject();
                if (orderData?["orderItems"] is not JsonArray items) continue;

                // Flight fare for this inventory segment (summed once per order).
                foreach (var item in items)
                {
                    if (item is not JsonObject oi) continue;
                    var invId = oi["inventoryId"]?.GetValue<string>();
                    if (!string.Equals(invId, inventoryIdStr, StringComparison.OrdinalIgnoreCase)) continue;
                    rev += (oi["baseFareAmount"]?.GetValue<decimal>() ?? 0m)
                         + (oi["taxAmount"]?.GetValue<decimal>() ?? 0m);
                    break;
                }

                // Ancillaries linked to this segment.
                foreach (var item in items)
                {
                    if (item is not JsonObject oi) continue;
                    var pt    = oi["productType"]?.GetValue<string>();
                    var segId = oi["segmentId"]?.GetValue<string>();
                    if (!string.Equals(segId, inventoryIdStr, StringComparison.OrdinalIgnoreCase)) continue;
                    if (pt is "SEAT" or "BAG" or "PRODUCT")
                        anc += oi["price"]?.GetValue<decimal>() ?? 0m;
                }
            }

            dayMap[dateKey] = (ordCnt, paxCnt, rev, anc);
        }

        var days = dayMap
            .OrderBy(kv => kv.Key)
            .Select(kv => new SalesPerformanceDayDto
            {
                Date             = kv.Key,
                OrderCount       = kv.Value.orderCount,
                PassengerCount   = kv.Value.passengerCount,
                Revenue          = Math.Round(kv.Value.revenue,  2, MidpointRounding.AwayFromZero),
                AncillaryRevenue = Math.Round(kv.Value.ancillary, 2, MidpointRounding.AwayFromZero),
            })
            .ToList();

        return new SalesPerformanceResponse
        {
            InventoryId = inventoryIdStr,
            Currency    = currency,
            TotalDays   = query.Days,
            Days        = days,
        };
    }
}
