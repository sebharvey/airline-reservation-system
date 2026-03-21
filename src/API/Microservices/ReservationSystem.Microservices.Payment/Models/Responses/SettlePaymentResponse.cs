namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentReference}/settle.
/// </summary>
public sealed class SettlePaymentResponse
{
    public Guid PaymentId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal AuthorisedAmount { get; init; }
    public decimal? SettledAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset AuthorisedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
