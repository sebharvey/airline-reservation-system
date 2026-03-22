using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

public sealed class UpdateBagPricingRequest
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTime ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
