namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// HTTP request body for the loyalty points transfer endpoint.
/// </summary>
public sealed class TransferPointsRequest
{
    public string RecipientLoyaltyNumber { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public int Points { get; init; }
}
