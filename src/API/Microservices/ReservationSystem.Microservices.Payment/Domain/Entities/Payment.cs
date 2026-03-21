namespace ReservationSystem.Microservices.Payment.Domain.Entities;

/// <summary>
/// Core domain entity representing a payment transaction.
/// Contains business state and enforces invariants.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Payment
{
    public Guid PaymentId { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
    public string? BookingReference { get; private set; }
    public string PaymentType { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string? CardType { get; private set; }
    public string? CardLast4 { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal AuthorisedAmount { get; private set; }
    public decimal? SettledAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset AuthorisedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Payment() { }

    /// <summary>
    /// Factory method for creating a brand-new payment. Assigns a new PaymentId and timestamps.
    /// </summary>
    public static Payment Create(
        string paymentReference,
        string? bookingReference,
        string paymentType,
        string method,
        string? cardType,
        string? cardLast4,
        string currencyCode,
        decimal authorisedAmount,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var now = DateTimeOffset.UtcNow;

        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            PaymentReference = paymentReference,
            BookingReference = bookingReference,
            PaymentType = paymentType,
            Method = method,
            CardType = cardType,
            CardLast4 = cardLast4,
            CurrencyCode = currencyCode,
            AuthorisedAmount = authorisedAmount,
            SettledAmount = null,
            Status = PaymentStatus.Authorised,
            AuthorisedAt = now,
            SettledAt = null,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// Does not assign a new PaymentId or reset timestamps.
    /// </summary>
    public static Payment Reconstitute(
        Guid paymentId,
        string paymentReference,
        string? bookingReference,
        string paymentType,
        string method,
        string? cardType,
        string? cardLast4,
        string currencyCode,
        decimal authorisedAmount,
        decimal? settledAmount,
        string status,
        DateTimeOffset authorisedAt,
        DateTimeOffset? settledAt,
        string? description,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Payment
        {
            PaymentId = paymentId,
            PaymentReference = paymentReference,
            BookingReference = bookingReference,
            PaymentType = paymentType,
            Method = method,
            CardType = cardType,
            CardLast4 = cardLast4,
            CurrencyCode = currencyCode,
            AuthorisedAmount = authorisedAmount,
            SettledAmount = settledAmount,
            Status = status,
            AuthorisedAt = authorisedAt,
            SettledAt = settledAt,
            Description = description,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Settle(decimal settledAmount)
    {
        SettledAmount = settledAmount;
        SettledAt = DateTimeOffset.UtcNow;
        Status = settledAmount < AuthorisedAmount
            ? PaymentStatus.PartiallySettled
            : PaymentStatus.Settled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Refund(decimal refundAmount)
    {
        Status = refundAmount < SettledAmount
            ? PaymentStatus.PartiallySettled
            : PaymentStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Allowed status values for a Payment. Kept adjacent to the entity to
/// avoid magic strings across the codebase.
/// </summary>
public static class PaymentStatus
{
    public const string Authorised = "Authorised";
    public const string Settled = "Settled";
    public const string PartiallySettled = "PartiallySettled";
    public const string Refunded = "Refunded";
    public const string Failed = "Failed";
    public const string Declined = "Declined";
    public const string Voided = "Voided";
}
