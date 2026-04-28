namespace ReservationSystem.Microservices.Offer.Application.UpdateInventoryAircraftType;

public sealed record UpdateInventoryAircraftTypeCommand(
    string FlightNumber,
    string DepartureDate,
    string NewAircraftType);
