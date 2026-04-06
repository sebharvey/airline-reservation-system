using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SellInventoryRequest
{
    [JsonPropertyName("items")]
    public IReadOnlyList<SellInventoryItemRequest> Items { get; init; } = [];

    [JsonPropertyName("paxCount")]
    public int PaxCount { get; init; }

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }
}

public sealed class SellInventoryItemRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;
}
