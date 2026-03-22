namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body representing a customer resource.
/// </summary>
public sealed class CustomerResponse
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public Guid? IdentityReference { get; init; }
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Nationality { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int TierProgressPoints { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
