using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Responses;

/// <summary>
/// Admin API response for a list of payments (includes pre-computed event count).
/// Returned by GET /v1/admin/payments?date=YYYY-MM-DD.
/// </summary>
public sealed class AdminPaymentListItemResponse
{
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("cardType")]
    public string? CardType { get; init; }

    [JsonPropertyName("cardLast4")]
    public string? CardLast4 { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("authorisedAmount")]
    public decimal? AuthorisedAmount { get; init; }

    [JsonPropertyName("settledAmount")]
    public decimal? SettledAmount { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("authorisedAt")]
    public DateTime? AuthorisedAt { get; init; }

    [JsonPropertyName("settledAt")]
    public DateTime? SettledAt { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("eventCount")]
    public int EventCount { get; init; }
}

/// <summary>
/// Admin API response for a single payment record.
/// Returned by GET /v1/admin/payments/{paymentId}.
/// </summary>
public sealed class AdminPaymentResponse
{
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("cardType")]
    public string? CardType { get; init; }

    [JsonPropertyName("cardLast4")]
    public string? CardLast4 { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("authorisedAmount")]
    public decimal? AuthorisedAmount { get; init; }

    [JsonPropertyName("settledAmount")]
    public decimal? SettledAmount { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("authorisedAt")]
    public DateTime? AuthorisedAt { get; init; }

    [JsonPropertyName("settledAt")]
    public DateTime? SettledAt { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}
