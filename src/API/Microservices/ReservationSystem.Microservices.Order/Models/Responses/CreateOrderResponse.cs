using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class CreateOrderResponse
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}
