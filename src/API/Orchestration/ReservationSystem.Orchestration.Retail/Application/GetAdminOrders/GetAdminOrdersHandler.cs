using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrders;

public sealed class GetAdminOrdersHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public GetAdminOrdersHandler(OrderServiceClient orderServiceClient, OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public async Task<IReadOnlyList<AdminOrderSummaryResponse>> HandleAsync(
        GetAdminOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await _orderServiceClient.GetRecentOrdersAsync(query.Limit, cancellationToken);

        var confirmed = orders
            .Where(o => !string.IsNullOrEmpty(o.BookingReference))
            .ToList();

        if (confirmed.Count == 0)
            return [];

        // Collect all unique inventoryIds referenced across all orders so we can
        // fetch flight details in a single parallel batch rather than N × M calls.
        var inventoryIdsByOrder = confirmed
            .Select(o => (order: o, ids: ExtractInventoryIds(o.OrderData)))
            .ToList();

        var allInventoryIds = inventoryIdsByOrder
            .SelectMany(x => x.ids)
            .Distinct()
            .ToList();

        // Fetch all needed flight details concurrently
        var fetchTasks = allInventoryIds
            .Select(id => _offerServiceClient.GetFlightByInventoryIdAsync(id, cancellationToken)
                .ContinueWith(t => (id, detail: t.IsCompletedSuccessfully ? t.Result : null),
                    TaskContinuationOptions.None));

        var fetched = await Task.WhenAll(fetchTasks);
        var flightCache = fetched
            .Where(x => x.detail is not null)
            .ToDictionary(x => x.id, x => x.detail!);

        return inventoryIdsByOrder
            .Select(x => ToSummary(x.order, x.ids, flightCache))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Extracts ordered inventoryIds from an order's orderItems array.</summary>
    private static List<Guid> ExtractInventoryIds(JsonElement? orderData)
    {
        var ids = new List<Guid>();
        if (!orderData.HasValue) return ids;

        try
        {
            var data = orderData.Value;
            if (data.TryGetProperty("orderItems", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("inventoryId", out var invEl) &&
                        invEl.TryGetGuid(out var id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }
        catch { }

        return ids;
    }

    private static AdminOrderSummaryResponse ToSummary(
        OrderMsOrderResult order,
        List<Guid> inventoryIds,
        Dictionary<Guid, Infrastructure.ExternalServices.Dto.FlightInventoryDetailDto> flightCache)
    {
        var leadName = string.Empty;
        var route = string.Empty;

        if (order.OrderData.HasValue)
        {
            try
            {
                var data = order.OrderData.Value;
                if (data.TryGetProperty("dataLists", out var dataLists) &&
                    dataLists.TryGetProperty("passengers", out var passengers) &&
                    passengers.GetArrayLength() > 0)
                {
                    var first = passengers[0];
                    var given = first.TryGetProperty("givenName", out var gn) ? gn.GetString() : "";
                    var surname = first.TryGetProperty("surname", out var sn) ? sn.GetString() : "";
                    leadName = $"{given} {surname}".Trim();
                }
            }
            catch { }
        }

        // Build route from first-segment origin → last-segment destination
        if (inventoryIds.Count > 0)
        {
            flightCache.TryGetValue(inventoryIds[0], out var firstFlight);
            flightCache.TryGetValue(inventoryIds[^1], out var lastFlight);

            if (firstFlight is not null && lastFlight is not null)
                route = $"{firstFlight.Origin} → {lastFlight.Destination}";
            else if (firstFlight is not null)
                route = $"{firstFlight.Origin} → {firstFlight.Destination}";
        }

        return new AdminOrderSummaryResponse
        {
            BookingReference = order.BookingReference ?? string.Empty,
            OrderStatus = order.OrderStatus,
            ChannelCode = order.ChannelCode,
            Currency = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            LeadPassengerName = leadName,
            Route = route,
        };
    }
}
