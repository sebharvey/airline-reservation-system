namespace ReservationSystem.Orchestration.Operations.Application.AutoAssignSeats;

public sealed record AutoAssignSeatsCommand(
    Guid InventoryId,
    string FlightNumber,
    string DepartureDate,
    string AircraftType);
