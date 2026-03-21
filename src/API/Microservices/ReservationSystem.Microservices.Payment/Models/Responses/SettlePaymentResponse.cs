namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentReference}/settle.
/// </summary>
public sealed class SettlePaymentResponse
{
    public string PaymentReference { get; init; } = string.Empty;
    public decimal SettledAmount { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
}
