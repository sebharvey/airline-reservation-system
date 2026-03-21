namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after successfully creating a customer.
/// </summary>
public sealed class CreateCustomerResponse
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
