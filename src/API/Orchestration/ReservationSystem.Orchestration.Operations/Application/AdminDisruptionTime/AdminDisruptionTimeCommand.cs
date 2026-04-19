namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionTime;

public sealed record AdminDisruptionTimeCommand(
    string FlightNumber,
    string DepartureDate,
    string NewDepartureTime,
    string NewArrivalTime,
    string? Reason);
