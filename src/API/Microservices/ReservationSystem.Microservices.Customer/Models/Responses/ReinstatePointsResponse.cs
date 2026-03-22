namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after reinstating points.
/// </summary>
public sealed class ReinstatePointsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int PointsReinstated { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTime ReinstatedAt { get; init; }
}
