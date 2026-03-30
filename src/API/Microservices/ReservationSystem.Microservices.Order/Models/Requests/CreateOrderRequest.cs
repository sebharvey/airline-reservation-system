using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class CreateOrderRequest
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("redemptionReference")]
    public string? RedemptionReference { get; init; }

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";
}

public sealed class PaymentReferenceItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("paymentReference")]
    public string PaymentReference { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }
}
