using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Bag;

public sealed class EfBagPricingRepository : IBagPricingRepository
{
    private readonly AncillaryDbContext _context;
    private readonly ILogger<EfBagPricingRepository> _logger;

    public EfBagPricingRepository(AncillaryDbContext context, ILogger<EfBagPricingRepository> logger)
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

    public async Task<BagPricing?> GetBySequenceAsync(int bagSequence, string currencyCode, CancellationToken cancellationToken = default)
    {
        return await _context.BagPricings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BagSequence == bagSequence && p.CurrencyCode == currencyCode, cancellationToken);
    }

    public async Task<IReadOnlyList<BagPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var pricings = await _context.BagPricings
            .AsNoTracking()
            .OrderBy(p => p.BagSequence)
            .ToListAsync(cancellationToken);
        return pricings.AsReadOnly();
    }

    public async Task<IReadOnlyList<BagPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var pricings = await _context.BagPricings
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.BagSequence)
            .ToListAsync(cancellationToken);
        return pricings.AsReadOnly();
    }

    public async Task<BagPricing> CreateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        _context.BagPricings.Add(pricing);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created BagPricing {PricingId} for sequence {BagSequence}", pricing.PricingId, pricing.BagSequence);
        return pricing;
    }

    public async Task<BagPricing?> UpdateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        _context.BagPricings.Update(pricing);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for BagPricing {PricingId}", pricing.PricingId);
            return null;
        }
        return pricing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.BagPricings
            .Where(p => p.PricingId == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
