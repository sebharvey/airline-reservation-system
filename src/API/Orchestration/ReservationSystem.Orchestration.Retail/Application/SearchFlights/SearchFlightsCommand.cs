namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

public sealed record SearchFlightsCommand(
    string Origin,
    string Destination,
    string DepartureDate,
    string CabinCode,
    int PaxCount,
    string BookingType);
