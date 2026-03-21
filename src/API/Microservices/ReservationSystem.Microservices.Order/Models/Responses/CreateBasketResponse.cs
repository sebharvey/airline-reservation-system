using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/basket (201 Created).
/// </summary>
public sealed class CreateBasketResponse
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("basketStatus")]
    public string BasketStatus { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
