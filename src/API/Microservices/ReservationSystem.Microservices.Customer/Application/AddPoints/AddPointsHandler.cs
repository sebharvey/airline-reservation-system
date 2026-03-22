using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.AddPoints;

/// <summary>
/// Handles the <see cref="AddPointsCommand"/>.
/// Adds points to a customer loyalty account as an Adjustment transaction.
/// </summary>
public sealed class AddPointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<AddPointsHandler> _logger;

    public AddPointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<AddPointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<LoyaltyTransaction?> HandleAsync(AddPointsCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return null;
        }

        customer.AddPoints(command.Points);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        var transaction = LoyaltyTransaction.Create(
            customerId: customer.CustomerId,
            transactionType: "Adjustment",
            pointsDelta: command.Points,
            balanceAfter: customer.PointsBalance,
            description: "Added initial points balance for testing");

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Added {Points} points for {LoyaltyNumber}", command.Points, command.LoyaltyNumber);

        return transaction;
    }
}
