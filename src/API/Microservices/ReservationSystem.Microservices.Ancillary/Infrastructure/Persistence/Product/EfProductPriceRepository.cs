using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Product;

public sealed class EfProductPriceRepository : IProductPriceRepository
{
    private readonly AncillaryDbContext _context;
    private readonly ILogger<EfProductPriceRepository> _logger;

    public EfProductPriceRepository(AncillaryDbContext context, ILogger<EfProductPriceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductPrice?> GetByIdAsync(Guid priceId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductPrices
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PriceId == priceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductPrice>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var prices = await _context.ProductPrices
            .AsNoTracking()
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.CurrencyCode)
            .ToListAsync(cancellationToken);
        return prices.AsReadOnly();
    }

    public async Task<ProductPrice> CreateAsync(ProductPrice price, CancellationToken cancellationToken = default)
    {
        _context.ProductPrices.Add(price);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created ProductPrice {PriceId} (OfferId={OfferId})", price.PriceId, price.OfferId);
        return price;
    }

    public async Task<ProductPrice?> UpdateAsync(ProductPrice price, CancellationToken cancellationToken = default)
    {
        _context.ProductPrices.Update(price);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for ProductPrice {PriceId}", price.PriceId);
            return null;
        }
        return price;
    }

    public async Task<bool> DeleteAsync(Guid priceId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.ProductPrices
            .Where(p => p.PriceId == priceId)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
