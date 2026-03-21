using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public async Task<LoyaltyTransaction?> HandleAsync(AuthorisePointsCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return null;
        }

        customer.DeductPoints(command.Points);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        var transaction = LoyaltyTransaction.Create(
            loyaltyNumber: command.LoyaltyNumber,
            transactionType: "Redeem",
            pointsDelta: -command.Points,
            balanceAfter: customer.PointsBalance,
            description: $"Points authorisation hold — basket {command.BasketId}");

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Authorised {Points} points for {LoyaltyNumber}", command.Points, command.LoyaltyNumber);

        return transaction;
    }
}
