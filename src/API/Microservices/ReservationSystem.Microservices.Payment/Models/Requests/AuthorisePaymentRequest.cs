namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/authorise.
/// </summary>
public sealed class AuthorisePaymentRequest
{
    public string? BookingReference { get; init; }
    public string PaymentType { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string? CardType { get; init; }
    public string? CardLast4 { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}
