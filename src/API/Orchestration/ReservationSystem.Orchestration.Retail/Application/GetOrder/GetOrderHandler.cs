using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetOrder;

public sealed class GetOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetOrderHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public Task<OrderResponse?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
