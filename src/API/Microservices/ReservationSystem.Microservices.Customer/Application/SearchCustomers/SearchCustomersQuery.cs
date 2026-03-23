namespace ReservationSystem.Microservices.Customer.Application.SearchCustomers;

/// <summary>
/// Query to search customers by loyalty number, given name, or surname.
/// Partial matches are supported on all fields.
/// </summary>
public sealed record SearchCustomersQuery(string SearchTerm);
