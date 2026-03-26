using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentId}/void.
/// </summary>
public sealed class VoidPaymentRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
