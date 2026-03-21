namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Core domain entity representing a loyalty points transaction.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class LoyaltyTransaction
{
    public Guid TransactionId { get; private set; }
    public string LoyaltyNumber { get; private set; } = string.Empty;
    public string TransactionType { get; private set; } = string.Empty;
    public int PointsDelta { get; private set; }
    public int BalanceAfter { get; private set; }
    public string? BookingReference { get; private set; }
    public string? FlightNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset TransactionDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private LoyaltyTransaction() { }

    /// <summary>
    /// Factory method for creating a brand-new loyalty transaction.
    /// </summary>
    public static LoyaltyTransaction Create(
        string loyaltyNumber,
        string transactionType,
        int pointsDelta,
        int balanceAfter,
        string description,
        string? bookingReference = null,
        string? flightNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loyaltyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new LoyaltyTransaction
        {
            TransactionId = Guid.NewGuid(),
            LoyaltyNumber = loyaltyNumber,
            TransactionType = transactionType,
            PointsDelta = pointsDelta,
            BalanceAfter = balanceAfter,
            BookingReference = bookingReference,
            FlightNumber = flightNumber,
            Description = description,
            TransactionDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static LoyaltyTransaction Reconstitute(
        Guid transactionId,
        string loyaltyNumber,
        string transactionType,
        int pointsDelta,
        int balanceAfter,
        string? bookingReference,
        string? flightNumber,
        string description,
        DateTimeOffset transactionDate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new LoyaltyTransaction
        {
            TransactionId = transactionId,
            LoyaltyNumber = loyaltyNumber,
            TransactionType = transactionType,
            PointsDelta = pointsDelta,
            BalanceAfter = balanceAfter,
            BookingReference = bookingReference,
            FlightNumber = flightNumber,
            Description = description,
            TransactionDate = transactionDate,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
