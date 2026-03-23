namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class PointsOperationResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public int PointsDelta { get; init; }
    public int NewBalance { get; init; }
    public string? BookingReference { get; init; }
    public DateTime TransactionDate { get; init; }
}
