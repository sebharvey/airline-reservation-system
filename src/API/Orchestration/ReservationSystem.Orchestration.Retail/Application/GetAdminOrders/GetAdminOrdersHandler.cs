using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrders;

public sealed class GetAdminOrdersHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetAdminOrdersHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<IReadOnlyList<AdminOrderSummaryResponse>> HandleAsync(
        GetAdminOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await _orderServiceClient.GetRecentOrdersAsync(query.Limit, cancellationToken);

        var confirmed = orders
            .Where(o => !string.IsNullOrEmpty(o.BookingReference))
            .ToList();

        return confirmed.Count == 0
            ? []
            : confirmed.Select(ToSummary).ToList().AsReadOnly();
    }

    private static AdminOrderSummaryResponse ToSummary(OrderMsOrderResult order)
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
                    var surname = (first.TryGetProperty("surname", out var sn) ? sn.GetString() : "")?.ToUpperInvariant().Trim() ?? "";
                    var given  = (first.TryGetProperty("givenName", out var gn) ? gn.GetString() : "")?.ToUpperInvariant().Trim() ?? "";
                    leadName = surname.Length > 0 && given.Length > 0 ? $"{surname}/{given}" : surname + given;
                }

                // origin/destination are stored on each orderItem — no need to call the Offer service.
                // Build route: origin → turnaround destination, skipping return legs.
                if (data.TryGetProperty("orderItems", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    var segments = new List<(string Origin, string Destination)>();
                    foreach (var item in items.EnumerateArray())
                    {
                        var origin = item.TryGetProperty("origin", out var o) ? o.GetString() ?? "" : "";
                        var dest   = item.TryGetProperty("destination", out var d) ? d.GetString() ?? "" : "";
                        if (origin.Length > 0 && dest.Length > 0)
                            segments.Add((origin, dest));
                    }

                    if (segments.Count > 0)
                    {
                        var firstOrigin = segments[0].Origin;
                        var displayDestination = segments[^1].Destination;
                        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { firstOrigin };

                        for (var i = 0; i < segments.Count; i++)
                        {
                            var dest = segments[i].Destination;
                            if (visited.Contains(dest))
                            {
                                displayDestination = i > 0 ? segments[i - 1].Destination : dest;
                                break;
                            }
                            visited.Add(dest);
                        }

                        route = $"{firstOrigin} → {displayDestination}";
                    }
                }
            }
            catch { }
        }

        return new AdminOrderSummaryResponse
        {
            BookingReference  = order.BookingReference ?? string.Empty,
            OrderStatus       = order.OrderStatus,
            ChannelCode       = order.ChannelCode,
            Currency          = order.CurrencyCode,
            TotalAmount       = order.TotalAmount,
            CreatedAt         = order.CreatedAt,
            LeadPassengerName = leadName,
            Route             = route,
        };
    }
}
