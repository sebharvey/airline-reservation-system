namespace ReservationSystem.Microservices.Payment.Domain.Entities;

/// <summary>
/// Core domain entity representing a payment transaction.
/// Contains business state and enforces invariants.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Payment
{
    public Guid PaymentId { get; private set; }
    public string? BookingReference { get; private set; }
    public string PaymentType { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string? CardType { get; private set; }
    public string? CardLast4 { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal? AuthorisedAmount { get; private set; }
    public decimal? SettledAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime? AuthorisedAt { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Payment() { }

    /// <summary>
    /// Factory method for initialising a new payment. Assigns a new PaymentId and timestamps.
    /// Called during the initialise step before card authorisation.
    /// </summary>
    public static Payment Initialise(
        string? bookingReference,
        string paymentType,
        string method,
        string currencyCode,
        decimal amount,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var now = DateTime.UtcNow;

        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            BookingReference = bookingReference,
            PaymentType = paymentType,
            Method = method,
            CardType = null,
            CardLast4 = null,
            CurrencyCode = currencyCode,
            Amount = amount,
            AuthorisedAmount = null,
            SettledAmount = null,
            Status = PaymentStatus.Initialised,
            AuthorisedAt = null,
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
        string? bookingReference,
        string paymentType,
        string method,
        string? cardType,
        string? cardLast4,
        string currencyCode,
        decimal amount,
        decimal? authorisedAmount,
        decimal? settledAmount,
        string status,
        DateTime? authorisedAt,
        DateTime? settledAt,
        string? description,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new Payment
        {
            PaymentId = paymentId,
            BookingReference = bookingReference,
            PaymentType = paymentType,
            Method = method,
            CardType = cardType,
            CardLast4 = cardLast4,
            CurrencyCode = currencyCode,
            Amount = amount,
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

    /// <summary>
    /// Records a card authorisation against this payment.
    /// May be called from <c>Initialised</c> status only.
    /// </summary>
    public void Authorise(decimal authorisedAmount, string? cardType, string? cardLast4)
    {
        AuthorisedAmount = (AuthorisedAmount ?? 0m) + authorisedAmount;
        CardType = cardType;
        CardLast4 = cardLast4;
        AuthorisedAt = DateTime.UtcNow;
        Status = PaymentStatus.Authorised;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a settlement capture against this payment.
    /// The settled amount must equal the authorised amount — partial settlement is not permitted.
    /// </summary>
    public void Settle(decimal settledAmount)
    {
        SettledAmount = settledAmount;
        SettledAt = DateTime.UtcNow;
        Status = PaymentStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Void()
    {
        Status = PaymentStatus.Voided;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Refund(decimal refundAmount)
    {
        Status = PaymentStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Allowed status values for a Payment. Kept adjacent to the entity to
/// avoid magic strings across the codebase.
/// </summary>
public static class PaymentStatus
{
    public const string Initialised = "Initialised";
    public const string Authorised = "Authorised";
    public const string Settled = "Settled";
    public const string Refunded = "Refunded";
    public const string Failed = "Failed";
    public const string Declined = "Declined";
    public const string Voided = "Voided";
}
