using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.SettlePoints;

/// <summary>
/// Handles the <see cref="SettlePointsCommand"/>.
/// Settles a previously authorised points transaction.
/// </summary>
public sealed class SettlePointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<SettlePointsHandler> _logger;

    public SettlePointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<SettlePointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public Task HandleAsync(SettlePointsCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
