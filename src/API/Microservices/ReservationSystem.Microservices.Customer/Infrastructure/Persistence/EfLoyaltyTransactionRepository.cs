using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ILoyaltyTransactionRepository"/>.
/// </summary>
public sealed class EfLoyaltyTransactionRepository : ILoyaltyTransactionRepository
{
    private readonly CustomerDbContext _context;
    private readonly ILogger<EfLoyaltyTransactionRepository> _logger;

    public EfLoyaltyTransactionRepository(
        CustomerDbContext context,
        ILogger<EfLoyaltyTransactionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<IReadOnlyList<LoyaltyTransaction>> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
