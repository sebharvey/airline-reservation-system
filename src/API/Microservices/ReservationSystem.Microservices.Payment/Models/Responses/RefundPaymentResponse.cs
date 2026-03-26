namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentId}/refund.
/// </summary>
public sealed class RefundPaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal RefundedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
}
