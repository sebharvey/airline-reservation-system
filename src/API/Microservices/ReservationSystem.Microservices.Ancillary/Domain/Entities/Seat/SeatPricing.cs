namespace ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;

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
    public decimal Tax { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SeatPricing() { }

    /// <summary>
    /// Factory method for creating a brand-new seat pricing rule.
    /// Tax is automatically calculated as 20% of price.
    /// </summary>
    public static SeatPricing Create(
        string cabinCode,
        string seatPosition,
        string currencyCode,
        decimal price,
        DateTime validFrom,
        DateTime? validTo = null)
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
            Tax = Math.Round(price * 0.20m, 2),
            IsActive = true,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
        decimal tax,
        bool isActive,
        DateTime validFrom,
        DateTime? validTo,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new SeatPricing
        {
            SeatPricingId = seatPricingId,
            CabinCode = cabinCode,
            SeatPosition = seatPosition,
            CurrencyCode = currencyCode,
            Price = price,
            Tax = tax,
            IsActive = isActive,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
