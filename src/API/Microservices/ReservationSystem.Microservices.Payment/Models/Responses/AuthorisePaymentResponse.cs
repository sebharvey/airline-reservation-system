namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/authorise.
/// </summary>
public sealed class AuthorisePaymentResponse
{
    public string PaymentReference { get; init; } = string.Empty;
    public decimal AuthorisedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
}
