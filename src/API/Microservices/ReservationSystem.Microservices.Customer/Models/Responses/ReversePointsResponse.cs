namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after reversing a points authorisation.
/// </summary>
public sealed class ReversePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsReleased { get; init; }
    public int NewPointsBalance { get; init; }
    public DateTime ReversedAt { get; init; }
}
