using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDetail;

public sealed class GetAdminOrderDetailHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetAdminOrderDetailHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<AdminOrderDetailResponse?> HandleAsync(string bookingReference, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, cancellationToken);
        if (order is null)
            return null;

        return new AdminOrderDetailResponse
        {
            OrderId = order.OrderId,
            BookingReference = order.BookingReference ?? bookingReference,
            OrderStatus = order.OrderStatus,
            ChannelCode = order.ChannelCode,
            CurrencyCode = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            TicketingTimeLimit = order.TicketingTimeLimit,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Version = order.Version,
            OrderData = order.OrderData,
        };
    }
}
