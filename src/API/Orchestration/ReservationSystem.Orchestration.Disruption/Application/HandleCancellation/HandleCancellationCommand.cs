namespace ReservationSystem.Orchestration.Disruption.Application.HandleCancellation;

public sealed record HandleCancellationCommand(
    string FlightNumber,
    DateTimeOffset ScheduledDeparture,
    string? Reason,
    bool EnableIropsRebooking);
