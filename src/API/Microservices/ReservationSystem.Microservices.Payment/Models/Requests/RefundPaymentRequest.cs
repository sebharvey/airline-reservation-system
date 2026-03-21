namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentReference}/refund.
/// </summary>
public sealed class RefundPaymentRequest
{
    public decimal Amount { get; init; }
    public string? Notes { get; init; }
}
