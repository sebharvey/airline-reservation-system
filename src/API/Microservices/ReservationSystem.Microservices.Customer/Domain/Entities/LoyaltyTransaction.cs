namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Core domain entity representing a loyalty points transaction.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class LoyaltyTransaction
{
    public Guid TransactionId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string TransactionType { get; private set; } = string.Empty;
    public int PointsDelta { get; private set; }
    public int BalanceAfter { get; private set; }
    public string? BookingReference { get; private set; }
    public string? FlightNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime TransactionDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private LoyaltyTransaction() { }

    /// <summary>
    /// Factory method for creating a brand-new loyalty transaction.
    /// </summary>
    public static LoyaltyTransaction Create(
        Guid customerId,
        string transactionType,
        int pointsDelta,
        int balanceAfter,
        string description,
        string? bookingReference = null,
        string? flightNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new LoyaltyTransaction
        {
            TransactionId = Guid.NewGuid(),
            CustomerId = customerId,
            TransactionType = transactionType,
            PointsDelta = pointsDelta,
            BalanceAfter = balanceAfter,
            BookingReference = bookingReference,
            FlightNumber = flightNumber,
            Description = description,
            TransactionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static LoyaltyTransaction Reconstitute(
        Guid transactionId,
        Guid customerId,
        string transactionType,
        int pointsDelta,
        int balanceAfter,
        string? bookingReference,
        string? flightNumber,
        string description,
        DateTime transactionDate,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new LoyaltyTransaction
        {
            TransactionId = transactionId,
            CustomerId = customerId,
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
