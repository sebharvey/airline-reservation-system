namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/initialise.
/// </summary>
public sealed class InitialisePaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
}
