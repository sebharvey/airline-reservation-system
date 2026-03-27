namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

/// <summary>
/// HTTP response body returned after a successful loyalty points transfer.
/// </summary>
public sealed class TransferPointsResponse
{
    public string SenderLoyaltyNumber { get; init; } = string.Empty;
    public string RecipientLoyaltyNumber { get; init; } = string.Empty;
    public int PointsTransferred { get; init; }
    public int SenderNewBalance { get; init; }
    public int RecipientNewBalance { get; init; }
    public DateTime TransferredAt { get; init; }
}
