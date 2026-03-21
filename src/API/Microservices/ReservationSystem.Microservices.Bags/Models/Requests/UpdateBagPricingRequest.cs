using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

public sealed class UpdateBagPricingRequest
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset? ValidTo { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
