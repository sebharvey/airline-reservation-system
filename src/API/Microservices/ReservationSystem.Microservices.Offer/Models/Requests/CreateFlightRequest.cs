namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class CreateFlightRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<CabinRequest> Cabins { get; init; } = [];
}

public sealed class CabinRequest
{
    public string CabinCode { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
}
