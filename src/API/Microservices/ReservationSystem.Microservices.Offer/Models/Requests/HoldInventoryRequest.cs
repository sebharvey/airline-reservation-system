using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class HoldInventoryRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("paxCount")]
    public int PaxCount { get; init; }

    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }
}
