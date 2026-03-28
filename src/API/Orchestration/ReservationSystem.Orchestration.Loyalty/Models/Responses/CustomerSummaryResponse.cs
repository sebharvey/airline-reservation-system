namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class CustomerSummaryResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
