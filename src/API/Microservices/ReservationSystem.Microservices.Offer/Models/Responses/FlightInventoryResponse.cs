namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FlightInventoryResponse
{
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<CabinInventoryResponse> Cabins { get; init; } = [];
}

public sealed class CabinInventoryResponse
{
    public string CabinCode { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
    public int SeatsSold { get; init; }
    public int SeatsHeld { get; init; }
}
