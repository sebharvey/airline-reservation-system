using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

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

    public Task<OrderResponse> HandleAsync(ConfirmBasketCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
