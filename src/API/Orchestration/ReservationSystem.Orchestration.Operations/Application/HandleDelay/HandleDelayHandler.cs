using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.HandleDelay;

public sealed class HandleDelayHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ILogger<HandleDelayHandler> _logger;

    public HandleDelayHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient,
        ILogger<HandleDelayHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
        _logger = logger;
    }

    public Task<DisruptionResponse> HandleAsync(HandleDelayCommand command, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "FOS delay received for flight {FlightNumber} departing {ScheduledDeparture} ({DelayMinutes} min, reason: {Reason}) — handler not yet implemented",
            command.FlightNumber,
            command.ScheduledDeparture,
            command.DelayMinutes,
            command.Reason ?? "none");

        throw new NotImplementedException();
    }
}
