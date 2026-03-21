using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/basket.
/// </summary>
public sealed class CreateBasketRequest
{
    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }
}
