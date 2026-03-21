using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Customer = ReservationSystem.Microservices.Customer.Domain.Entities.Customer;
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

    public async Task<Customer?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.LoyaltyNumber == loyaltyNumber, cancellationToken);
    }

    public async Task CreateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted Customer {CustomerId} into [customer].[Customer]", customer.CustomerId);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
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

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted Customer {CustomerId} from [customer].[Customer]", customer.CustomerId);
    }
}
