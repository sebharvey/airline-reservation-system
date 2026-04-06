using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class ReleaseInventoryRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("releaseType")]
    public string ReleaseType { get; init; } = string.Empty;

    [JsonPropertyName("basketId")]
    public Guid? BasketId { get; init; }
}
