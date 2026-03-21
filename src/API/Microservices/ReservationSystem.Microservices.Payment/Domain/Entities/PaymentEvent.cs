namespace ReservationSystem.Microservices.Payment.Domain.Entities;

/// <summary>
/// Domain entity representing an event that occurred against a payment (e.g. authorise, settle, refund).
/// Provides an audit trail of all payment lifecycle transitions.
/// </summary>
public sealed class PaymentEvent
{
    public Guid PaymentEventId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PaymentEvent() { }

    /// <summary>
    /// Factory method for creating a new payment event.
    /// </summary>
    public static PaymentEvent Create(
        Guid paymentId,
        string eventType,
        decimal amount,
        string currencyCode,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var now = DateTimeOffset.UtcNow;

        return new PaymentEvent
        {
            PaymentEventId = Guid.NewGuid(),
            PaymentId = paymentId,
            EventType = eventType,
            Amount = amount,
            CurrencyCode = currencyCode,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting a payment event from a persistence store.
    /// </summary>
    public static PaymentEvent Reconstitute(
        Guid paymentEventId,
        Guid paymentId,
        string eventType,
        decimal amount,
        string currencyCode,
        string? notes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new PaymentEvent
        {
            PaymentEventId = paymentEventId,
            PaymentId = paymentId,
            EventType = eventType,
            Amount = amount,
            CurrencyCode = currencyCode,
            Notes = notes,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

/// <summary>
/// Allowed event type values for a PaymentEvent.
/// </summary>
public static class PaymentEventType
{
    public const string Authorised = "Authorised";
    public const string Settled = "Settled";
    public const string PartialSettlement = "PartialSettlement";
    public const string Refunded = "Refunded";
    public const string Declined = "Declined";
    public const string Voided = "Voided";
}
