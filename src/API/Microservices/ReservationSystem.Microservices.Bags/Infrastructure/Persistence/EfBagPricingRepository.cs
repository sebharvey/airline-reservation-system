using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IBagPricingRepository"/>.
/// Uses <see cref="BagsDbContext"/> to interact with the [bag].[BagPricing] table.
/// </summary>
public sealed class EfBagPricingRepository : IBagPricingRepository
{
    private readonly BagsDbContext _context;
    private readonly ILogger<EfBagPricingRepository> _logger;

    public EfBagPricingRepository(BagsDbContext context, ILogger<EfBagPricingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BagPricing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BagPricings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PricingId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BagPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var pricings = await _context.BagPricings
            .AsNoTracking()
            .OrderBy(p => p.BagSequence)
            .ThenBy(p => p.CurrencyCode)
            .ToListAsync(cancellationToken);

        return pricings.AsReadOnly();
    }

    public async Task CreateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        _context.BagPricings.Add(pricing);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted BagPricing {PricingId} into [bag].[BagPricing]", pricing.PricingId);
    }

    public async Task UpdateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        _context.BagPricings.Update(pricing);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for BagPricing {PricingId}", pricing.PricingId);
        else
            _logger.LogDebug("Updated BagPricing {PricingId} in [bag].[BagPricing]", pricing.PricingId);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pricing = await _context.BagPricings
            .FirstOrDefaultAsync(p => p.PricingId == id, cancellationToken);

        if (pricing is null)
            return;

        _context.BagPricings.Remove(pricing);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted BagPricing {PricingId} from [bag].[BagPricing]", id);
    }
}
