using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed class CreateBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public CreateBasketHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public Task<BasketResponse> HandleAsync(CreateBasketCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
