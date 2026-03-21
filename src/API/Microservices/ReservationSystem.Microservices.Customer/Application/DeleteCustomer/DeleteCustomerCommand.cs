namespace ReservationSystem.Microservices.Customer.Application.DeleteCustomer;

/// <summary>
/// Command carrying the loyalty number needed to delete a Customer.
/// </summary>
public sealed record DeleteCustomerCommand(string LoyaltyNumber);
