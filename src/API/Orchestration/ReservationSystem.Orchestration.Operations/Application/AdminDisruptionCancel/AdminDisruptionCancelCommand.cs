namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;

public sealed record AdminDisruptionCancelCommand(
    string FlightNumber,
    string DepartureDate,
    string? Reason);
