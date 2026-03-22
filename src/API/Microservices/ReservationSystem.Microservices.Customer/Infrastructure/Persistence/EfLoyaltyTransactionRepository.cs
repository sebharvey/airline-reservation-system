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
        var customerId = await ResolveCustomerIdAsync(loyaltyNumber, cancellationToken);
        if (customerId is null)
            return Array.Empty<LoyaltyTransaction>().AsReadOnly();

        var transactions = await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId.Value)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        return transactions.AsReadOnly();
    }

    public async Task<(IReadOnlyList<LoyaltyTransaction> Transactions, int TotalCount)> GetByLoyaltyNumberAsync(string loyaltyNumber, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveCustomerIdAsync(loyaltyNumber, cancellationToken);
        if (customerId is null)
            return (Array.Empty<LoyaltyTransaction>().AsReadOnly(), 0);

        var query = _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (transactions.AsReadOnly(), totalCount);
    }

    public async Task<LoyaltyTransaction?> FindAuthorisationHoldAsync(string loyaltyNumber, string redemptionReference, CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveCustomerIdAsync(loyaltyNumber, cancellationToken);
        if (customerId is null)
            return null;

        return await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId.Value
                && t.TransactionType == "Redeem"
                && t.Description.Contains(redemptionReference))
            .OrderByDescending(t => t.TransactionDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
    {
        _context.LoyaltyTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted LoyaltyTransaction {TransactionId} into [customer].[LoyaltyTransaction]", transaction.TransactionId);
    }

    private async Task<Guid?> ResolveCustomerIdAsync(string loyaltyNumber, CancellationToken cancellationToken)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.LoyaltyNumber == loyaltyNumber)
            .Select(c => (Guid?)c.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
