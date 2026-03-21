namespace ReservationSystem.Microservices.Customer.Application.UpdateCustomer;

/// <summary>
/// Command carrying the data needed to update an existing Customer.
/// </summary>
public sealed record UpdateCustomerCommand(
    string LoyaltyNumber,
    string? GivenName,
    string? Surname,
    DateOnly? DateOfBirth,
    string? Nationality,
    string? PhoneNumber,
    string? PreferredLanguage);
