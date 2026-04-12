using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Product;

public sealed class EfProductRepository : IProductRepository
{
    private readonly AncillaryDbContext _context;
    private readonly ILogger<EfProductRepository> _logger;

    public EfProductRepository(AncillaryDbContext context, ILogger<EfProductRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Domain.Entities.Product.Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Prices)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Entities.Product.Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _context.Products
            .Include(p => p.Prices)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return products.AsReadOnly();
    }

    public async Task<IReadOnlyList<Domain.Entities.Product.Product>> GetByGroupAsync(Guid productGroupId, CancellationToken cancellationToken = default)
    {
        var products = await _context.Products
            .Include(p => p.Prices)
            .AsNoTracking()
            .Where(p => p.ProductGroupId == productGroupId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return products.AsReadOnly();
    }

    public async Task<Domain.Entities.Product.Product> CreateAsync(Domain.Entities.Product.Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created Product {ProductId}", product.ProductId);
        return product;
    }

    public async Task<Domain.Entities.Product.Product?> UpdateAsync(Domain.Entities.Product.Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(product);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for Product {ProductId}", product.ProductId);
            return null;
        }
        return product;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Products
            .Where(p => p.ProductId == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
