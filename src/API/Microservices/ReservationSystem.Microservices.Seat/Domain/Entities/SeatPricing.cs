namespace ReservationSystem.Microservices.Seat.Domain.Entities;

/// <summary>
/// Core domain entity representing a seat pricing rule.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class SeatPricing
{
    public Guid SeatPricingId { get; private set; }
    public string CabinCode { get; private set; } = string.Empty;
    public string SeatPosition { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SeatPricing() { }

    /// <summary>
    /// Factory method for creating a brand-new seat pricing rule.
    /// </summary>
    public static SeatPricing Create(
        string cabinCode,
        string seatPosition,
        string currencyCode,
        decimal price,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(seatPosition);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return new SeatPricing
        {
            SeatPricingId = Guid.NewGuid(),
            CabinCode = cabinCode,
            SeatPosition = seatPosition,
            CurrencyCode = currencyCode,
            Price = price,
            IsActive = true,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static SeatPricing Reconstitute(
        Guid seatPricingId,
        string cabinCode,
        string seatPosition,
        string currencyCode,
        decimal price,
        bool isActive,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new SeatPricing
        {
            SeatPricingId = seatPricingId,
            CabinCode = cabinCode,
            SeatPosition = seatPosition,
            CurrencyCode = currencyCode,
            Price = price,
            IsActive = isActive,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
