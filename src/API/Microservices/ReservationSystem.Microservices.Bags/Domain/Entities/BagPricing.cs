namespace ReservationSystem.Microservices.Bags.Domain.Entities;

/// <summary>
/// Core domain entity representing the pricing for an additional bag.
/// Keyed by (BagSequence, CurrencyCode) with temporal validity.
/// </summary>
public sealed class BagPricing
{
    public Guid PricingId { get; private set; }
    public int BagSequence { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BagPricing() { }

    /// <summary>
    /// Factory method for creating a brand-new bag pricing entry. Assigns a new Id and timestamps.
    /// </summary>
    public static BagPricing Create(int bagSequence, decimal price, string currencyCode, DateTimeOffset validFrom, DateTimeOffset? validTo)
    {
        return new BagPricing
        {
            PricingId = Guid.NewGuid(),
            BagSequence = bagSequence,
            Price = price,
            CurrencyCode = currencyCode,
            ValidFrom = validFrom,
            ValidTo = validTo,
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
        int bagSequence,
        decimal price,
        string currencyCode,
        bool isActive,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new BagPricing
        {
            PricingId = pricingId,
            BagSequence = bagSequence,
            Price = price,
            CurrencyCode = currencyCode,
            IsActive = isActive,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
