namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for updating an existing customer.
/// </summary>
public sealed class UpdateCustomerRequest
{
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
    public string? PassportNumber { get; init; }
    public DateOnly? PassportIssueDate { get; init; }
    public string? PassportIssuer { get; init; }
    public string? KnownTravellerNumber { get; init; }
    public Guid? IdentityId { get; init; }
    public string? TierCode { get; init; }
    public bool? IsActive { get; init; }
}
