namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class FlightInventoryGroupDto
{
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public CabinInventoryDto? F { get; init; }
    public CabinInventoryDto? J { get; init; }
    public CabinInventoryDto? W { get; init; }
    public CabinInventoryDto? Y { get; init; }
    public int TotalSeats { get; init; }
    public int TotalSeatsAvailable { get; init; }
    public int LoadFactor { get; init; }
    public string TicketingStatus { get; init; } = string.Empty;
}

public sealed class CabinInventoryDto
{
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
    public int SeatsSold { get; init; }
    public int SeatsHeld { get; init; }
}
