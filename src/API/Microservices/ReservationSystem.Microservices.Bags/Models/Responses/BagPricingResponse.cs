using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Responses;

/// <summary>
/// HTTP response body for BagPricing endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class BagPricingResponse
{
    [JsonPropertyName("pricingId")]
    public Guid PricingId { get; init; }

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

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
