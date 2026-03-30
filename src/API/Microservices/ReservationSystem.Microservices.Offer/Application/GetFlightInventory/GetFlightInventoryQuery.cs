namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventory;

public sealed record GetFlightInventoryQuery(string FlightNumber, DateOnly DepartureDate);
