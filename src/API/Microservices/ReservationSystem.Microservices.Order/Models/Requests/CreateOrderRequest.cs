using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/orders.
/// Confirms a basket and creates an order.
/// </summary>
public sealed class CreateOrderRequest
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }
}
