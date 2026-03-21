namespace ReservationSystem.Microservices.Offer.Application.CreateFlight;

public sealed record CreateFlightCommand(
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
