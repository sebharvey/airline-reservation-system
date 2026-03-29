namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class InventoryStatusResponse
{
    public Guid InventoryId { get; init; }
    public int SeatsAvailable { get; init; }
    public IReadOnlyList<CabinInventoryResponse> Cabins { get; init; } = [];
}

public sealed class SellInventoryResponse
{
    public IReadOnlyList<InventoryStatusResponse> Sold { get; init; } = [];
}

public sealed class CancelInventoryResponse
{
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public int InventoriesCancelled { get; init; }
    public string Status { get; init; } = string.Empty;
}
