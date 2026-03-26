namespace ReservationSystem.Microservices.Payment.Domain.Entities;

/// <summary>
/// Domain entity representing a payment event tracking the lifecycle of a payment.
/// Created at authorisation and updated on settle or void.
/// </summary>
public sealed class PaymentEvent
{
    public Guid PaymentEventId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PaymentEvent() { }

    /// <summary>
    /// Factory method for creating a new payment event at authorisation time.
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

        var now = DateTime.UtcNow;

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
        DateTime createdAt,
        DateTime updatedAt)
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

    /// <summary>
    /// Updates the event on settlement or void.
    /// </summary>
    public void Update(string eventType, decimal amount, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        EventType = eventType;
        Amount = amount;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
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
