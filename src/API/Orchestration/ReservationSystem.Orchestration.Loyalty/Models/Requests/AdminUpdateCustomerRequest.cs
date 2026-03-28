namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class AdminUpdateCustomerRequest
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
}
