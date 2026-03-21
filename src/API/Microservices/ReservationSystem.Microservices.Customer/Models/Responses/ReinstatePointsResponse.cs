namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after reinstating points.
/// </summary>
public sealed class ReinstatePointsResponse
{
    public Guid TransactionId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public int PointsDelta { get; init; }
    public int BalanceAfter { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
}
