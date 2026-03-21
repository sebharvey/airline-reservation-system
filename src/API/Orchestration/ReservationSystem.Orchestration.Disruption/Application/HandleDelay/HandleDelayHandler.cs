using ReservationSystem.Orchestration.Disruption.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Disruption.Models.Responses;

namespace ReservationSystem.Orchestration.Disruption.Application.HandleDelay;

public sealed class HandleDelayHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public HandleDelayHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public Task<DisruptionResponse> HandleAsync(HandleDelayCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
