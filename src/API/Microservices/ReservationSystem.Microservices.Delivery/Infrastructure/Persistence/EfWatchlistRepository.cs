using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

public sealed class EfWatchlistRepository : IWatchlistRepository
{
    private readonly DeliveryDbContext _context;
    private readonly ILogger<EfWatchlistRepository> _logger;

    public EfWatchlistRepository(DeliveryDbContext context, ILogger<EfWatchlistRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _context.WatchlistEntries
            .AsNoTracking()
            .OrderBy(e => e.Surname)
            .ThenBy(e => e.GivenName)
            .ToListAsync(cancellationToken);
        return entries.AsReadOnly();
    }

    public async Task<WatchlistEntry?> GetByIdAsync(Guid watchlistId, CancellationToken cancellationToken = default)
    {
        return await _context.WatchlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.WatchlistId == watchlistId, cancellationToken);
    }

    public async Task<WatchlistEntry?> GetByPassportNumberAsync(string passportNumber, CancellationToken cancellationToken = default)
    {
        return await _context.WatchlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PassportNumber == passportNumber, cancellationToken);
    }

    public async Task<WatchlistEntry> CreateAsync(WatchlistEntry entry, CancellationToken cancellationToken = default)
    {
        _context.WatchlistEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<WatchlistEntry?> UpdateAsync(WatchlistEntry entry, CancellationToken cancellationToken = default)
    {
        _context.WatchlistEntries.Update(entry);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for WatchlistEntry {WatchlistId}", entry.WatchlistId);
            return null;
        }
        return entry;
    }

    public async Task<bool> DeleteAsync(Guid watchlistId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.WatchlistEntries
            .Where(e => e.WatchlistId == watchlistId)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
