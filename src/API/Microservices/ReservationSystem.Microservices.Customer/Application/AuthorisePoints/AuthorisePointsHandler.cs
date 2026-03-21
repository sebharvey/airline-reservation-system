using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.AuthorisePoints;

/// <summary>
/// Handles the <see cref="AuthorisePointsCommand"/>.
/// Authorises a points transaction for a customer loyalty account.
/// </summary>
public sealed class AuthorisePointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<AuthorisePointsHandler> _logger;

    public AuthorisePointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<AuthorisePointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public Task HandleAsync(AuthorisePointsCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
