using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public Task<Customer?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
