namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentReference}/refund.
/// </summary>
public sealed class RefundPaymentResponse
{
    public Guid PaymentId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal AuthorisedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
