using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<LoyaltyTransaction>> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default)
    {
        var transactions = await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyNumber == loyaltyNumber)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        return transactions.AsReadOnly();
    }

    public async Task<(IReadOnlyList<LoyaltyTransaction> Transactions, int TotalCount)> GetByLoyaltyNumberAsync(string loyaltyNumber, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyNumber == loyaltyNumber);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (transactions.AsReadOnly(), totalCount);
    }

    public async Task CreateAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
    {
        _context.LoyaltyTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted LoyaltyTransaction {TransactionId} into [customer].[LoyaltyTransaction]", transaction.TransactionId);
    }
}
