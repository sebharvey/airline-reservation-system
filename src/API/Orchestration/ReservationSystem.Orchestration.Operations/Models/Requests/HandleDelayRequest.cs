namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class HandleDelayRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public DateTime ScheduledDeparture { get; init; }
    public int DelayMinutes { get; init; }
    public string? Reason { get; init; }
}
