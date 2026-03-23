namespace ReservationSystem.Microservices.Customer.Application.GetCustomer;

/// <summary>
/// Query carrying the identity ID needed to retrieve a Customer.
/// </summary>
public sealed record GetCustomerByIdentityQuery(Guid IdentityId);
