namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/payment/authorise.
/// </summary>
public sealed class AuthorisePaymentResponse
{
    public Guid PaymentId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string? BookingReference { get; init; }
    public string PaymentType { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string? CardType { get; init; }
    public string? CardLast4 { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal AuthorisedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset AuthorisedAt { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
