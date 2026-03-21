namespace ReservationSystem.Microservices.Offer.Application.CancelInventory;

public sealed record CancelInventoryCommand(
    string FlightNumber,
    string DepartureDate);
