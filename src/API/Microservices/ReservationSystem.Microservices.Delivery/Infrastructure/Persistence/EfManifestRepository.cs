using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IManifestRepository"/>.
/// </summary>
public sealed class EfManifestRepository : IManifestRepository
{
    private readonly DeliveryDbContext _context;
    private readonly ILogger<EfManifestRepository> _logger;

    public EfManifestRepository(
        DeliveryDbContext context,
        ILogger<EfManifestRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default)
    {
        return await _context.Manifests
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ManifestId == manifestId, cancellationToken);
    }

    public async Task CreateAsync(Manifest manifest, CancellationToken cancellationToken = default)
    {
        _context.Manifests.Add(manifest);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Manifest {ManifestId} into [delivery].[Manifest]", manifest.ManifestId);
    }

    public async Task UpdateAsync(Manifest manifest, CancellationToken cancellationToken = default)
    {
        _context.Manifests.Update(manifest);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Manifest {ManifestId}", manifest.ManifestId);
    }
}
