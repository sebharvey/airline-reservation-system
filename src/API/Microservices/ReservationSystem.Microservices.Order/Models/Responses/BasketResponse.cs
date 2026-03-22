using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

/// <summary>
/// HTTP response body for Basket endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class BasketResponse
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("basketStatus")]
    public string BasketStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalFareAmount")]
    public decimal? TotalFareAmount { get; init; }

    [JsonPropertyName("totalSeatAmount")]
    public decimal TotalSeatAmount { get; init; }

    [JsonPropertyName("totalBagAmount")]
    public decimal TotalBagAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }

    [JsonPropertyName("confirmedOrderId")]
    public Guid? ConfirmedOrderId { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}
