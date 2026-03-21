namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentReference}/refund.
/// </summary>
public sealed class RefundPaymentResponse
{
    public string PaymentReference { get; init; } = string.Empty;
    public decimal RefundedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
}
