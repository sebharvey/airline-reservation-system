namespace ReservationSystem.Microservices.Order.Application.QueryOrders;

/// <summary>
/// Query to retrieve all orders for a specific flight.
/// </summary>
public sealed record QueryOrdersQuery(string FlightNumber, string DepartureDate, string? Status);
