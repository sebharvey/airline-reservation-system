namespace ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;

public sealed record BatchCreateFlightsCommand(IReadOnlyList<BatchFlightItem> Items);

public sealed record BatchFlightItem(
    string FlightNumber,
    string DepartureDate,
    string DepartureTime,
    string ArrivalTime,
    int ArrivalDayOffset,
    string Origin,
    string Destination,
    string AircraftType,
    string CabinCode,
    int TotalSeats);
