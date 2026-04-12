using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Bag.Requests;

public sealed class UpdateBagPricingRequest
{
    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTime ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
