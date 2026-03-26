namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/{paymentId}/void.
/// </summary>
public sealed class VoidPaymentResponse
{
    public Guid PaymentId { get; init; }
    public string Status { get; init; } = string.Empty;
}
