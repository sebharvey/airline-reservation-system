using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.ReinstatePoints;

/// <summary>
/// Handles the <see cref="ReinstatePointsCommand"/>.
/// Reinstates previously reversed points on a customer loyalty account.
/// </summary>
public sealed class ReinstatePointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<ReinstatePointsHandler> _logger;

    public ReinstatePointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<ReinstatePointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public Task HandleAsync(ReinstatePointsCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
