namespace ReservationSystem.Microservices.Bags.Domain.Entities;

/// <summary>
/// Core domain entity representing the pricing for an additional bag.
/// Associates a cabin class with its extra bag fee and currency.
/// </summary>
public sealed class BagPricing
{
    public Guid PricingId { get; private set; }
    public string CabinCode { get; private set; } = string.Empty;
    public int BagNumber { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BagPricing() { }

    /// <summary>
    /// Factory method for creating a brand-new bag pricing entry. Assigns a new Id and timestamps.
    /// </summary>
    public static BagPricing Create(string cabinCode, int bagNumber, decimal price, string currency)
    {
        return new BagPricing
        {
            PricingId = Guid.NewGuid(),
            CabinCode = cabinCode,
            BagNumber = bagNumber,
            Price = price,
            Currency = currency,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting a bag pricing entry from a persistence store.
    /// Does not assign a new Id or reset timestamps.
    /// </summary>
    public static BagPricing Reconstitute(
        Guid pricingId,
        string cabinCode,
        int bagNumber,
        decimal price,
        string currency,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new BagPricing
        {
            PricingId = pricingId,
            CabinCode = cabinCode,
            BagNumber = bagNumber,
            Price = price,
            Currency = currency,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
