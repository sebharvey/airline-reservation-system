namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after adding points to a customer's loyalty balance.
/// </summary>
public sealed class AddPointsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int PointsAdded { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTime AddedAt { get; init; }
}
