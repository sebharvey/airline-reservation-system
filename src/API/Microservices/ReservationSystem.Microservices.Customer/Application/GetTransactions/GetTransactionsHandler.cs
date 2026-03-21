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

    public async Task<IReadOnlyList<LoyaltyTransaction>> HandleAsync(
        GetTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
