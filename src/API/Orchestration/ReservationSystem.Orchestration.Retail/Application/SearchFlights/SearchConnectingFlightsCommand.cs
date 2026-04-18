namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

public sealed record SearchConnectingFlightsCommand(
    string Origin,
    string Destination,
    string DepartureDate,
    int PaxCount,
    string BookingType,
    bool IncludePrivateFares = false);
