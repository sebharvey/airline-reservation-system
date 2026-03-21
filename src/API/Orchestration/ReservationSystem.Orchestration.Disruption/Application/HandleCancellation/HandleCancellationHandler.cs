using ReservationSystem.Orchestration.Disruption.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Disruption.Models.Responses;

namespace ReservationSystem.Orchestration.Disruption.Application.HandleCancellation;

public sealed class HandleCancellationHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public HandleCancellationHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public Task<DisruptionResponse> HandleAsync(HandleCancellationCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
