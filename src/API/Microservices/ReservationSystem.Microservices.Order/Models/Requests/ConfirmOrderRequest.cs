using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class ConfirmOrderRequest
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("paymentReferences")]
    public List<PaymentReferenceItem>? PaymentReferences { get; init; }
}
