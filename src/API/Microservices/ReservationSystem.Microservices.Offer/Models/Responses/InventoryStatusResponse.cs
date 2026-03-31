using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class InventoryStatusResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("cabins")]
    public IReadOnlyList<CabinInventoryResponse> Cabins { get; init; } = [];
}

public sealed class SellInventoryResponse
{
    [JsonPropertyName("sold")]
    public IReadOnlyList<InventoryStatusResponse> Sold { get; init; } = [];
}

public sealed class CancelInventoryResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("inventoriesCancelled")]
    public int InventoriesCancelled { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
