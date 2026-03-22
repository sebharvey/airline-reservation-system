namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for adding points to a customer's loyalty balance.
/// </summary>
public sealed class AddPointsRequest
{
    public int Points { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
