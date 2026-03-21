namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

public sealed record SearchFlightsCommand(
    string Origin,
    string Destination,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    int PassengerCount,
    string? CabinClass);
