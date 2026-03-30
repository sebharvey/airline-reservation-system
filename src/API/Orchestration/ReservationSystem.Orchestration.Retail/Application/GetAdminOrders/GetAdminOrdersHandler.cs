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

        return orders
            .Where(o => !string.IsNullOrEmpty(o.BookingReference))
            .Select(ToSummary)
            .ToList()
            .AsReadOnly();
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
                if (data.TryGetProperty("dataLists", out var dataLists))
                {
                    if (dataLists.TryGetProperty("passengers", out var passengers) &&
                        passengers.GetArrayLength() > 0)
                    {
                        var first = passengers[0];
                        var given = first.TryGetProperty("givenName", out var gn) ? gn.GetString() : "";
                        var surname = first.TryGetProperty("surname", out var sn) ? sn.GetString() : "";
                        leadName = $"{given} {surname}".Trim();
                    }

                    if (dataLists.TryGetProperty("flightSegments", out var segments) &&
                        segments.GetArrayLength() > 0)
                    {
                        var first = segments[0];
                        var last = segments[segments.GetArrayLength() - 1];
                        var origin = first.TryGetProperty("origin", out var orig) ? orig.GetString() : "";
                        var dest = last.TryGetProperty("destination", out var dst) ? dst.GetString() : "";
                        route = $"{origin} → {dest}";
                    }
                }
            }
            catch { }
        }

        return new AdminOrderSummaryResponse
        {
            BookingReference = order.BookingReference ?? string.Empty,
            OrderStatus = order.OrderStatus,
            ChannelCode = order.ChannelCode,
            CurrencyCode = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            LeadPassengerName = leadName,
            Route = route,
        };
    }
}
