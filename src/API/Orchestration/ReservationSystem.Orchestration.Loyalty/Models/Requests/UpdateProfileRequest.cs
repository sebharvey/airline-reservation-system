namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// Request body for PATCH /v1/customers/{loyaltyNumber}/profile.
/// Fields identityId, tier, active, and points are not permitted and are ignored.
/// </summary>
public sealed class UpdateProfileRequest
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
    public DateOnly? PassportExpiryDate { get; init; }
    public string? KnownTravellerNumber { get; init; }
}
