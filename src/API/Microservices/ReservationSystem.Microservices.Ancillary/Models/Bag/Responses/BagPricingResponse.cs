using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Bag.Responses;

public sealed class BagPricingResponse
{
    [JsonPropertyName("pricingId")]
    public Guid PricingId { get; init; }

    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTime ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Wrapper response for GET /v1/bag-pricing list endpoint.
/// </summary>
public sealed class BagPricingListResponse
{
    [JsonPropertyName("pricing")]
    public IReadOnlyList<BagPricingResponse> Pricing { get; init; } = [];
}
