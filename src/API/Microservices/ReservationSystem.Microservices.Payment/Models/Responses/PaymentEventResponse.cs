namespace ReservationSystem.Microservices.Payment.Models.Responses;

/// <summary>
/// Response body for a single event in GET /v1/payment/{paymentId}/events.
/// Represents one row from payment.PaymentEvent ordered chronologically.
/// </summary>
public sealed class PaymentEventResponse
{
    public Guid PaymentEventId { get; init; }
    public Guid PaymentId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}
