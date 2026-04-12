using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Bag;

public sealed class EfBagPolicyRepository : IBagPolicyRepository
{
    private readonly AncillaryDbContext _context;
    private readonly ILogger<EfBagPolicyRepository> _logger;

    public EfBagPolicyRepository(AncillaryDbContext context, ILogger<EfBagPolicyRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BagPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BagPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PolicyId == id, cancellationToken);
    }

    public async Task<BagPolicy?> GetByCabinCodeAsync(string cabinCode, CancellationToken cancellationToken = default)
    {
        return await _context.BagPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CabinCode == cabinCode, cancellationToken);
    }

    public async Task<IReadOnlyList<BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _context.BagPolicies
            .AsNoTracking()
            .OrderBy(p => p.CabinCode)
            .ToListAsync(cancellationToken);
        return policies.AsReadOnly();
    }

    public async Task<BagPolicy> CreateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.BagPolicies.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created BagPolicy {PolicyId} for cabin {CabinCode}", policy.PolicyId, policy.CabinCode);
        return policy;
    }

    public async Task<BagPolicy?> UpdateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.BagPolicies.Update(policy);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateAsync found no row for BagPolicy {PolicyId}", policy.PolicyId);
            return null;
        }
        return policy;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.BagPolicies
            .Where(p => p.PolicyId == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rowsAffected > 0;
    }
}
