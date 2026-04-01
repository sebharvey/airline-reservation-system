using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Infrastructure.Persistence;

public sealed class EfSsrCatalogueRepository : ISsrCatalogueRepository
{
    private readonly OrderDbContext _context;

    public EfSsrCatalogueRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SsrCatalogueEntry>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SsrCatalogue
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Category)
            .ThenBy(e => e.SsrCode)
            .ToListAsync(cancellationToken);
    }
}
