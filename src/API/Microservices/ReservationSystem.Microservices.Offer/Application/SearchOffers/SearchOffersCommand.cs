namespace ReservationSystem.Microservices.Offer.Application.SearchOffers;

public sealed record SearchOffersCommand(
    string Origin,
    string Destination,
    string DepartureDate,
    string? CabinCode,
    int PaxCount,
    string BookingType);
