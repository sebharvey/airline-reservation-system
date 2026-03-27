namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for transferring points from one loyalty account to another.
/// </summary>
public sealed class TransferPointsRequest
{
    public string RecipientLoyaltyNumber { get; init; } = string.Empty;
    public int Points { get; init; }
}
