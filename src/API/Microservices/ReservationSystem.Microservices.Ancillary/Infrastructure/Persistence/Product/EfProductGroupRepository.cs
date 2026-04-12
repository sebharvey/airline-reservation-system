using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Product;

public sealed class EfProductGroupRepository : IProductGroupRepository
{
    private readonly AncillaryDbContext _context;
    private readonly ILogger<EfProductGroupRepository> _logger;

    public EfProductGroupRepository(AncillaryDbContext context, ILogger<EfProductGroupRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProductGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.ProductGroupId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _context.ProductGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
        return groups.AsReadOnly();
    }

    public async Task<ProductGroup> CreateAsync(ProductGroup group, CancellationToken cancellationToken = default)
    {
        _context.ProductGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created ProductGroup {ProductGroupId}", group.ProductGroupId);
        return group;
    }

    public async Task<ProductGroup?> UpdateAsync(ProductGroup group, CancellationToken cancellationToken = default)
    {
        _context.ProductGroups.Update(group);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for ProductGroup {ProductGroupId}", group.ProductGroupId);
            return null;
        }
        return group;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.ProductGroups
            .Where(g => g.ProductGroupId == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
