namespace ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;

public sealed record UpdateProfileCommand(
    string LoyaltyNumber,
    string? GivenName,
    string? Surname,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? PhoneNumber,
    string? PreferredLanguage,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateOrRegion,
    string? PostalCode,
    string? CountryCode,
    string? PassportNumber,
    DateOnly? PassportIssueDate,
    string? PassportIssuer,
    DateOnly? PassportExpiryDate,
    string? KnownTravellerNumber);
