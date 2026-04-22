using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.HandleCancellation;

public sealed class HandleCancellationHandler
{
    private readonly AdminDisruptionCancelHandler _cancelHandler;
    private readonly ILogger<HandleCancellationHandler> _logger;

    public HandleCancellationHandler(
        AdminDisruptionCancelHandler cancelHandler,
        ILogger<HandleCancellationHandler> logger)
    {
        _cancelHandler = cancelHandler;
        _logger = logger;
    }

    public async Task<DisruptionResponse> HandleAsync(HandleCancellationCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "FOS cancellation received for flight {FlightNumber} departing {ScheduledDeparture} (reason: {Reason}, iropsRebooking: {EnableIropsRebooking})",
            command.FlightNumber,
            command.ScheduledDeparture,
            command.Reason ?? "none",
            command.EnableIropsRebooking);

        var adminCommand = new AdminDisruptionCancelCommand(
            command.FlightNumber,
            command.ScheduledDeparture.ToString("yyyy-MM-dd"),
            command.Reason);

        var result = await _cancelHandler.HandleAsync(adminCommand, cancellationToken);

        return new DisruptionResponse
        {
            DisruptionId = Guid.NewGuid(),
            FlightNumber = result.FlightNumber,
            DisruptionType = "Cancellation",
            Status = result.FailedCount == 0 ? "Completed" : "CompletedWithErrors",
            AffectedBookings = result.AffectedPassengerCount,
            NotificationsSent = 0,
            RebookingsInitiated = result.RebookedCount,
            ProcessedAt = result.ProcessedAt
        };
    }
}
