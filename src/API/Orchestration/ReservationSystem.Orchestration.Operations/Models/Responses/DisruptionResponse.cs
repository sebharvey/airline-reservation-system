namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class DisruptionResponse
{
    public Guid DisruptionId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DisruptionType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AffectedBookings { get; init; }
    public int NotificationsSent { get; init; }
    public int RebookingsInitiated { get; init; }
    public DateTime ProcessedAt { get; init; }
}
