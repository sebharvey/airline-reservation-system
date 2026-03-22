namespace ReservationSystem.Microservices.Bags.Domain.Entities;

/// <summary>
/// Core domain entity representing the pricing for an additional bag.
/// Pricing is per bag sequence (1st additional, 2nd additional, 99=catch-all),
/// fleet-wide and route-agnostic.
/// </summary>
public sealed class BagPricing
{
    public Guid PricingId { get; private set; }
    public int BagSequence { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private BagPricing() { }

    public static BagPricing Create(int bagSequence, string currencyCode, decimal price,
        DateTime validFrom, DateTime? validTo)
    {
        return new BagPricing
        {
            PricingId = Guid.NewGuid(),
            BagSequence = bagSequence,
            CurrencyCode = currencyCode,
            Price = price,
            IsActive = true,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static BagPricing Reconstitute(
        Guid pricingId, int bagSequence, string currencyCode, decimal price,
        bool isActive, DateTime validFrom, DateTime? validTo,
        DateTime createdAt, DateTime updatedAt)
    {
        return new BagPricing
        {
            PricingId = pricingId,
            BagSequence = bagSequence,
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
