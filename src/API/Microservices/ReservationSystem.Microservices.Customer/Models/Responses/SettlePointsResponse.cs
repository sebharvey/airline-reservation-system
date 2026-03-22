namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after settling a points redemption.
/// </summary>
public sealed class SettlePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsDeducted { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTime SettledAt { get; init; }
}
