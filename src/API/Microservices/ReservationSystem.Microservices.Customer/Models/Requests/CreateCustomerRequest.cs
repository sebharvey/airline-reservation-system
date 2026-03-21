namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for creating a new customer.
/// </summary>
public sealed class CreateCustomerRequest
{
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public Guid? IdentityReference { get; init; }
}
