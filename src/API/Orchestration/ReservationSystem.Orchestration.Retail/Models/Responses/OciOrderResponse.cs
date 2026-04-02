namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class OciOrderResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<OciPassenger> Passengers { get; init; } = [];
    public IReadOnlyList<OciFlightSegment> FlightSegments { get; init; } = [];
}

public sealed class OciPassenger
{
    public string PassengerId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
}

public sealed class OciFlightSegment
{
    public string SegmentRef { get; init; } = string.Empty;
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureDateTime { get; init; }
    public DateTime ArrivalDateTime { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<OciSeatAssignment> SeatAssignments { get; init; } = [];
}

public sealed class OciSeatAssignment
{
    public string PassengerId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
}
