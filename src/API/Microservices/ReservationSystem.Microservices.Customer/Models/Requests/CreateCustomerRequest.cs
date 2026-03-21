namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for creating a new customer.
/// </summary>
public sealed class CreateCustomerRequest
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
    public Guid? IdentityReference { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Nationality { get; init; }
    public string? PhoneNumber { get; init; }
}
