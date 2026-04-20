using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.HandleCancellation;

public sealed class HandleCancellationHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ILogger<HandleCancellationHandler> _logger;

    public HandleCancellationHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient,
        ILogger<HandleCancellationHandler> logger)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
        _logger = logger;
    }

    public Task<DisruptionResponse> HandleAsync(HandleCancellationCommand command, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "FOS cancellation received for flight {FlightNumber} departing {ScheduledDeparture} (reason: {Reason}, iropsRebooking: {EnableIropsRebooking}) — handler not yet implemented",
            command.FlightNumber,
            command.ScheduledDeparture,
            command.Reason ?? "none",
            command.EnableIropsRebooking);

        throw new NotImplementedException();
    }
}
