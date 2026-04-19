using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Responses;

/// <summary>
/// Admin API response for a single payment event.
/// Returned by GET /v1/admin/payments/{paymentId}/events.
/// </summary>
public sealed class AdminPaymentEventResponse
{
    [JsonPropertyName("paymentEventId")]
    public Guid PaymentEventId { get; init; }

    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("productType")]
    public string ProductType { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}
