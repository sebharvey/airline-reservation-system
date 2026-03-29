namespace ReservationSystem.Microservices.Offer.Domain.Entities;

/// <summary>
/// Read-model representing flight inventory aggregated across all cabin classes for a
/// single flight on a given date. Used for the admin inventory view only.
/// </summary>
public sealed class FlightInventoryGroup
{
    public string FlightNumber { get; init; } = string.Empty;
    public DateOnly DepartureDate { get; init; }
    public TimeOnly DepartureTime { get; init; }
    public TimeOnly ArrivalTime { get; init; }
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    public CabinData? F { get; init; }
    public CabinData? J { get; init; }
    public CabinData? W { get; init; }
    public CabinData? Y { get; init; }

    public int TotalSeats { get; init; }
    public int TotalSeatsAvailable { get; init; }

    public sealed class CabinData
    {
        public int TotalSeats { get; init; }
        public int SeatsAvailable { get; init; }
        public int SeatsSold { get; init; }
        public int SeatsHeld { get; init; }
    }
}
