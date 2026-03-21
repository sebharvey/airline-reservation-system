using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/bag-pricing/{pricingId}.
/// </summary>
public sealed class UpdateBagPricingRequest
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("bagNumber")]
    public int BagNumber { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
