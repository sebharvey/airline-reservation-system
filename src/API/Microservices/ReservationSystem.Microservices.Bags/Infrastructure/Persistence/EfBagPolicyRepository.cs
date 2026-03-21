using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IBagPolicyRepository"/>.
/// Uses <see cref="BagsDbContext"/> to interact with the [bag].[BagPolicy] table.
/// </summary>
public sealed class EfBagPolicyRepository : IBagPolicyRepository
{
    private readonly BagsDbContext _context;
    private readonly ILogger<EfBagPolicyRepository> _logger;

    public EfBagPolicyRepository(BagsDbContext context, ILogger<EfBagPolicyRepository> logger)
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

    public async Task<IReadOnlyList<BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _context.BagPolicies
            .AsNoTracking()
            .OrderBy(p => p.CabinCode)
            .ToListAsync(cancellationToken);

        return policies.AsReadOnly();
    }

    public async Task CreateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.BagPolicies.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted BagPolicy {PolicyId} into [bag].[BagPolicy]", policy.PolicyId);
    }

    public async Task UpdateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.BagPolicies.Update(policy);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for BagPolicy {PolicyId}", policy.PolicyId);
        else
            _logger.LogDebug("Updated BagPolicy {PolicyId} in [bag].[BagPolicy]", policy.PolicyId);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await _context.BagPolicies
            .FirstOrDefaultAsync(p => p.PolicyId == id, cancellationToken);

        if (policy is null)
            return;

        _context.BagPolicies.Remove(policy);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted BagPolicy {PolicyId} from [bag].[BagPolicy]", id);
    }
}
