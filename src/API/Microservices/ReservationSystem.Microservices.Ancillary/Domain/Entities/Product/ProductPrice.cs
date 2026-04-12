namespace ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

/// <summary>
/// A per-currency price for an ancillary <see cref="Product"/>.
/// Each price has an auto-generated OfferId (GUID) that is unique per product+currency combination
/// and is used when the product is added to an order as a stored offer.
/// </summary>
public sealed class ProductPrice
{
    public Guid PriceId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid OfferId { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal Tax { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ProductPrice() { }

    public static ProductPrice Create(Guid productId, string currencyCode, decimal price, decimal tax) =>
        new()
        {
            PriceId = Guid.NewGuid(),
            ProductId = productId,
            OfferId = Guid.NewGuid(),
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Price = price,
            Tax = tax,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static ProductPrice Reconstitute(
        Guid priceId, Guid productId, Guid offerId, string currencyCode,
        decimal price, decimal tax, bool isActive, DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            PriceId = priceId,
            ProductId = productId,
            OfferId = offerId,
            CurrencyCode = currencyCode,
            Price = price,
            Tax = tax,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
