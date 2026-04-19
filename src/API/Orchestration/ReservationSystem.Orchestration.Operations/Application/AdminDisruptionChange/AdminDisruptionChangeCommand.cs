namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionChange;

public sealed record AdminDisruptionChangeCommand(
    string FlightNumber,
    string DepartureDate,
    string NewAircraftType,
    string? Reason);
