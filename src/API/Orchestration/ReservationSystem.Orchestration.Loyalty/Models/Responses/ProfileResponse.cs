namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class ProfileResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
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
    public string Tier { get; init; } = string.Empty;
    public decimal PointsBalance { get; init; }
    public DateTime MemberSince { get; init; }
}
