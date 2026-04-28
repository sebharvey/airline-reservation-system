namespace ReservationSystem.Microservices.Offer.Application.SetInventoryOperationalData;

public sealed record SetInventoryOperationalDataCommand(
    Guid InventoryId,
    string? DepartureGate,
    string? AircraftRegistration);
