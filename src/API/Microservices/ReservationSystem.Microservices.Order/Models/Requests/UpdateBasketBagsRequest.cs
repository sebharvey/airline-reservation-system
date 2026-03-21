using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/basket/{basketId}/bags.
/// </summary>
public sealed class UpdateBasketBagsRequest
{
    [JsonPropertyName("bagsData")]
    public string BagsData { get; init; } = string.Empty;
}
