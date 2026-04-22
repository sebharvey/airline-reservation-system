namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionGetOrders;

public sealed record AdminDisruptionGetOrdersQuery(
    string FlightNumber,
    string DepartureDate);
