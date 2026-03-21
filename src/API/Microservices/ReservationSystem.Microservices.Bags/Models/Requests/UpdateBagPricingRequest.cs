using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/bag-pricing/{pricingId}.
/// Per spec, only price, validity, and isActive are updatable.
/// </summary>
public sealed class UpdateBagPricingRequest
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset? ValidTo { get; init; }
}
