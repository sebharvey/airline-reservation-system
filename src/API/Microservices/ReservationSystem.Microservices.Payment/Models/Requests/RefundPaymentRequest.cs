using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentReference}/refund.
/// </summary>
public sealed class RefundPaymentRequest
{
    [JsonPropertyName("refundAmount")]
    public decimal RefundAmount { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}
