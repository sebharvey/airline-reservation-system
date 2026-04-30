namespace ReservationSystem.Microservices.Order.Application.GetIropsOrders;

/// <summary>
/// Query to retrieve all orders on a flight projected for IROPS processing.
/// </summary>
public sealed record GetIropsOrdersQuery(string FlightNumber, string DepartureDate, string? Status);
