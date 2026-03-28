namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class AdminAddPointsRequest
{
    public int Points { get; init; }
    public string Description { get; init; } = string.Empty;
}
