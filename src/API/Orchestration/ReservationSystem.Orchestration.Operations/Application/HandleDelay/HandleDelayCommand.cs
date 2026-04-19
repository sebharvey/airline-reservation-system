namespace ReservationSystem.Orchestration.Operations.Application.HandleDelay;

public sealed record HandleDelayCommand(
    string FlightNumber,
    DateTimeOffset ScheduledDeparture,
    int DelayMinutes,
    string? Reason);
