using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/orders (201 Created).
/// </summary>
public sealed class CreateOrderResponse
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
