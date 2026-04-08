namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class FlightInventoryDetailDto
{
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class FlightInventoryGroupDto
{
    public Guid InventoryId { get; init; }
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

public sealed class FlightInventoryHoldDto
{
    public Guid HoldId { get; init; }
    public Guid OrderId { get; init; }
    public string? PassengerId { get; init; }
    public string? BookingReference { get; init; }
    public string? PassengerName { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string? SeatNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public string HoldType { get; init; } = "Revenue";
    public short? StandbyPriority { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
