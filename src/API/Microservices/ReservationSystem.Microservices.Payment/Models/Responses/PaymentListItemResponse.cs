namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// Response body for GET /v1/payment?date=YYYY-MM-DD.
/// Extends the standard payment record with a pre-computed event count
/// so callers avoid N+1 round-trips when rendering a list.
/// </summary>
public sealed class PaymentListItemResponse
{
    public Guid PaymentId { get; init; }
    public string? BookingReference { get; init; }
    public string Method { get; init; } = string.Empty;
    public string? CardType { get; init; }
    public string? CardLast4 { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? AuthorisedAmount { get; init; }
    public decimal? SettledAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? AuthorisedAt { get; init; }
    public DateTime? SettledAt { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int EventCount { get; init; }
}
