namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionRebookOrder;

public sealed record AdminDisruptionRebookOrderCommand(
    string BookingReference,
    string FlightNumber,
    string DepartureDate,
    string? Reason);
