namespace ReservationSystem.Microservices.Customer.Application.GetCustomer;

/// <summary>
/// Query carrying the loyalty number needed to retrieve a Customer.
/// </summary>
public sealed record GetCustomerQuery(string LoyaltyNumber);
