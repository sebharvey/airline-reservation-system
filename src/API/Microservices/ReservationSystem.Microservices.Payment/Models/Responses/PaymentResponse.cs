namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// Response body for GET /v1/payment/{paymentId}.
/// Returns the full payment record including current status and financial totals.
/// </summary>
public sealed class PaymentResponse
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
}
