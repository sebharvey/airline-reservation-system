using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/bag-pricing.
/// </summary>
public sealed class CreateBagPricingRequest
{
    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset? ValidTo { get; init; }
}
