namespace ReservationSystem.Orchestration.Disruption.Models.Requests;

public sealed class HandleCancellationRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public DateTime ScheduledDeparture { get; init; }
    public string? Reason { get; init; }
    public bool EnableIropsRebooking { get; init; } = true;
}
