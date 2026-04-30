namespace ReservationSystem.Microservices.Delivery.Application.GetManifest;

public sealed record GetManifestQuery(string FlightNumber, DateOnly DepartureDate);
