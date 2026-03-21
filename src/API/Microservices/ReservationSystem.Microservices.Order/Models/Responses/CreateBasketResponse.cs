using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class CreateBasketResponse
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("basketStatus")]
    public string BasketStatus { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}
