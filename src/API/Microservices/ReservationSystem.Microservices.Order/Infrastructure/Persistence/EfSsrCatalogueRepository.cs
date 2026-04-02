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

    public async Task<SsrCatalogueEntry?> GetByCodeAsync(string ssrCode, CancellationToken cancellationToken = default)
    {
        return await _context.SsrCatalogue
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SsrCode == ssrCode, cancellationToken);
    }

    public async Task<SsrCatalogueEntry> CreateAsync(SsrCatalogueEntry entry, CancellationToken cancellationToken = default)
    {
        _context.SsrCatalogue.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<SsrCatalogueEntry?> UpdateAsync(string ssrCode, string label, string category, CancellationToken cancellationToken = default)
    {
        var entry = await _context.SsrCatalogue
            .FirstOrDefaultAsync(e => e.SsrCode == ssrCode, cancellationToken);
        if (entry is null) return null;

        entry.Label = label;
        entry.Category = category;
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<bool> DeactivateAsync(string ssrCode, CancellationToken cancellationToken = default)
    {
        var entry = await _context.SsrCatalogue
            .FirstOrDefaultAsync(e => e.SsrCode == ssrCode, cancellationToken);
        if (entry is null) return false;

        entry.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
