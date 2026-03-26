namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentId}/settle.
/// </summary>
public sealed class SettlePaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal SettledAmount { get; init; }
    public DateTime? SettledAt { get; init; }
}
