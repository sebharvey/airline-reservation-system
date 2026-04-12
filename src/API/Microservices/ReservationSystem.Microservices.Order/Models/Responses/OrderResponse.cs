using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class OrderResponse
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("ticketingTimeLimit")]
    public DateTime? TicketingTimeLimit { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("orderData")]
    public JsonElement? OrderData { get; init; }
}
