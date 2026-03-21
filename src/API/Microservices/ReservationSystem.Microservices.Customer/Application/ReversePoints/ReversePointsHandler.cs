using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.ReversePoints;

/// <summary>
/// Handles the <see cref="ReversePointsCommand"/>.
/// Reverses a points transaction on a customer loyalty account.
/// </summary>
public sealed class ReversePointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<ReversePointsHandler> _logger;

    public ReversePointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<ReversePointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public Task HandleAsync(ReversePointsCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
