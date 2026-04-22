namespace ReservationSystem.Microservices.Offer.Application.GetFlightAvailability;

public sealed record GetFlightAvailabilityQuery(
    string Origin,
    string Destination,
    DateOnly FromDate,
    DateOnly ToDate);
