using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ICustomerPreferencesRepository"/>.
/// </summary>
public sealed class EfCustomerPreferencesRepository : ICustomerPreferencesRepository
{
    private readonly CustomerDbContext _context;
    private readonly ILogger<EfCustomerPreferencesRepository> _logger;

    public EfCustomerPreferencesRepository(
        CustomerDbContext context,
        ILogger<EfCustomerPreferencesRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CustomerPreferences?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.CustomerPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CustomerId == customerId, cancellationToken);
    }

    public async Task<CustomerPreferences> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CustomerPreferences
            .FirstOrDefaultAsync(p => p.CustomerId == customerId, cancellationToken);

        if (existing is not null)
            return existing;

        var created = CustomerPreferences.Create(customerId);
        _context.CustomerPreferences.Add(created);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created default Preferences for Customer {CustomerId}", customerId);
        return created;
    }

    public async Task UpdateAsync(CustomerPreferences preferences, CancellationToken cancellationToken = default)
    {
        _context.CustomerPreferences.Update(preferences);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Updated Preferences for Customer {CustomerId}", preferences.CustomerId);
    }

    public async Task DeleteByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await _context.CustomerPreferences
            .Where(p => p.CustomerId == customerId)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogDebug("Deleted Preferences for Customer {CustomerId}", customerId);
    }
}
