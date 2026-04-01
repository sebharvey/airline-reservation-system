namespace ReservationSystem.Microservices.Customer.Application.UpdateCustomer;

/// <summary>
/// Command carrying the data needed to update an existing Customer.
/// </summary>
public sealed record UpdateCustomerCommand(
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
    string? KnownTravellerNumber,
    Guid? IdentityId,
    string? TierCode,
    bool? IsActive);
