using ReservationSystem.Orchestration.Retail.Models;

namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

public sealed record SearchFlightsCommand(
    string Origin,
    string Destination,
    string DepartureDate,
    int PaxCount,
    string BookingType,
    bool IncludePrivateFares = false,
    CustomerContext? CustomerContext = null);
