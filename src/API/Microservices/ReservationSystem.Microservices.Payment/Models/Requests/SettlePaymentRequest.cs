using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentReference}/settle.
/// </summary>
public sealed class SettlePaymentRequest
{
    [JsonPropertyName("settledAmount")]
    public decimal SettledAmount { get; init; }
}
