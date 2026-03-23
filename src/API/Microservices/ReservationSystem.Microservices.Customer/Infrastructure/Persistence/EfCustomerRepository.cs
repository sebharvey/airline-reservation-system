using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ICustomerRepository"/>.
/// </summary>
public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly CustomerDbContext _context;
    private readonly ILogger<EfCustomerRepository> _logger;

    public EfCustomerRepository(
        CustomerDbContext context,
        ILogger<EfCustomerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Domain.Entities.Customer?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.LoyaltyNumber == loyaltyNumber, cancellationToken);
    }

    public async Task<Domain.Entities.Customer?> GetByIdentityIdAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityId == identityId, cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Entities.Customer>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];

        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.LoyaltyNumber == searchTerm
                     || c.GivenName.Contains(searchTerm)
                     || c.Surname.Contains(searchTerm)
                     || (c.GivenName + " " + c.Surname).Contains(searchTerm))
            .OrderBy(c => c.Surname)
            .ThenBy(c => c.GivenName)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(Domain.Entities.Customer customer, CancellationToken cancellationToken = default)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted Customer {CustomerId} into [customer].[Customer]", customer.CustomerId);
    }

    public async Task UpdateAsync(Domain.Entities.Customer customer, CancellationToken cancellationToken = default)
    {
        _context.Customers.Update(customer);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Customer {CustomerId}", customer.CustomerId);
        else
            _logger.LogDebug("Updated Customer {CustomerId} in [customer].[Customer]", customer.CustomerId);
    }

    public async Task DeleteAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.LoyaltyNumber == loyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("DeleteAsync found no row for LoyaltyNumber {LoyaltyNumber}", loyaltyNumber);
            return;
        }

        var transactionCount = await _context.LoyaltyTransactions
            .Where(t => t.CustomerId == customer.CustomerId)
            .ExecuteDeleteAsync(cancellationToken);

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted Customer {CustomerId} and {TransactionCount} transaction(s) from [customer].[Customer]", customer.CustomerId, transactionCount);
    }
}
