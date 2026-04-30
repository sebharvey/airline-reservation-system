namespace ReservationSystem.Microservices.Delivery.Application.DeleteManifestFlight;

public sealed record DeleteManifestFlightCommand(string BookingReference, string FlightNumber, DateOnly DepartureDate);
