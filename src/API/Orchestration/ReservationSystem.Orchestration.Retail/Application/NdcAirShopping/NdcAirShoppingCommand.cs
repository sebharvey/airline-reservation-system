namespace ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

/// <summary>
/// Parsed parameters extracted from an IATA NDC 21.3 AirShoppingRQ.
/// </summary>
public sealed record NdcAirShoppingCommand(
    string Origin,
    string Destination,
    string DepartureDate,
    int TotalPaxCount,
    IReadOnlyList<NdcPassengerType> Passengers);

/// <summary>
/// A passenger type group from the NDC Travelers element.
/// </summary>
public sealed record NdcPassengerType(string Ptc, int Quantity);
