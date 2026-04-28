namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class InventoryOrdersResponse
{
    public string InventoryId { get; init; } = string.Empty;
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public InventoryOrdersCabinsDto? Cabins { get; init; }
    public IReadOnlyList<InventoryOrderRowDto> Orders { get; init; } = [];
}

public sealed class InventoryOrdersCabinsDto
{
    public CabinCountDto? F { get; init; }
    public CabinCountDto? J { get; init; }
    public CabinCountDto? W { get; init; }
    public CabinCountDto? Y { get; init; }
}

public sealed class CabinCountDto
{
    public int TotalSeats { get; init; }
    public int SeatsSold { get; init; }
    public int SeatsAvailable { get; init; }
    public int SeatsHeld { get; init; }
}

public sealed class InventoryOrderRowDto
{
    public string OrderId { get; init; } = string.Empty;
    public string BookingReference { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string? PassengerName { get; init; }
    public string? PassengerType { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string? SeatNumber { get; init; }
    public string? FareFamily { get; init; }
    public string? FareBasisCode { get; init; }
    public decimal? BaseFareAmount { get; init; }
    public decimal? TaxAmount { get; init; }
    public decimal? TotalFareAmount { get; init; }
    public IReadOnlyList<InventoryOrderAncillaryDto> Ancillaries { get; init; } = [];
}

public sealed class InventoryOrderAncillaryDto
{
    public string ProductType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
