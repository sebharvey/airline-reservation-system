namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class AdminCustomerResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int TierProgressPoints { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
