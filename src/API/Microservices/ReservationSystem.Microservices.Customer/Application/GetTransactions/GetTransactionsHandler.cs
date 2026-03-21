using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetTransactions;

/// <summary>
/// Handles the <see cref="GetTransactionsQuery"/>.
/// </summary>
public sealed class GetTransactionsHandler
{
    private readonly ILoyaltyTransactionRepository _repository;
    private readonly ILogger<GetTransactionsHandler> _logger;

    public GetTransactionsHandler(
        ILoyaltyTransactionRepository repository,
        ILogger<GetTransactionsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<LoyaltyTransaction> Transactions, int TotalCount)> HandleAsync(
        GetTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var (transactions, totalCount) = await _repository.GetByLoyaltyNumberAsync(
            query.LoyaltyNumber, query.Page, query.PageSize, cancellationToken);

        _logger.LogDebug("Retrieved {Count} transactions (page {Page}) for {LoyaltyNumber}",
            transactions.Count, query.Page, query.LoyaltyNumber);

        return (transactions, totalCount);
    }
}
