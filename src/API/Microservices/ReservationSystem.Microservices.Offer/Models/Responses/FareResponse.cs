using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FareResponse
{
    [JsonPropertyName("fareId")]
    public Guid FareId { get; init; }

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }
}
