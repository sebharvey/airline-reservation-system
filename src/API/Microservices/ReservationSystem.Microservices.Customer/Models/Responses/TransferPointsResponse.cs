namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after a successful points transfer between two loyalty accounts.
/// </summary>
public sealed class TransferPointsResponse
{
    public string SenderLoyaltyNumber { get; init; } = string.Empty;
    public string RecipientLoyaltyNumber { get; init; } = string.Empty;
    public int PointsTransferred { get; init; }
    public int SenderNewBalance { get; init; }
    public int RecipientNewBalance { get; init; }
    public Guid SenderTransactionId { get; init; }
    public Guid RecipientTransactionId { get; init; }
    public DateTime TransferredAt { get; init; }
}
