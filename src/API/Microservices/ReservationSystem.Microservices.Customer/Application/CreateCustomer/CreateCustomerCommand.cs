namespace ReservationSystem.Microservices.Customer.Application.CreateCustomer;

/// <summary>
/// Command carrying the data needed to create a new Customer.
/// </summary>
public sealed record CreateCustomerCommand(
    string LoyaltyNumber,
    string GivenName,
    string Surname,
    DateOnly? DateOfBirth,
    string PreferredLanguage,
    Guid? IdentityReference);
