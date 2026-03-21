namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for updating an existing customer.
/// </summary>
public sealed class UpdateCustomerRequest
{
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? TierCode { get; init; }
    public Guid? IdentityReference { get; init; }
    public string? PhoneNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Nationality { get; init; }
}
