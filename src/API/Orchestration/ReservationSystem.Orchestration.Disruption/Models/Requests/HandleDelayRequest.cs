namespace ReservationSystem.Orchestration.Disruption.Models.Requests;

public sealed class HandleDelayRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public DateTimeOffset ScheduledDeparture { get; init; }
    public int DelayMinutes { get; init; }
    public string? Reason { get; init; }
}
