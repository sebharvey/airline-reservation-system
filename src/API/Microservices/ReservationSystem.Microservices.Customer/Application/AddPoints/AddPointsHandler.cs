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

    private static readonly IReadOnlySet<string> ValidTransactionTypes =
        new HashSet<string>(StringComparer.Ordinal) { "Earn", "Redeem", "Adjustment", "Expiry", "Reinstate" };

    public async Task<LoyaltyTransaction?> HandleAsync(AddPointsCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidTransactionTypes.Contains(command.TransactionType))
            throw new ArgumentException(
                $"Invalid transaction type '{command.TransactionType}'. Must be one of: {string.Join(", ", ValidTransactionTypes)}.",
                nameof(command));

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
            transactionType: command.TransactionType,
            pointsDelta: command.Points,
            balanceAfter: customer.PointsBalance,
            description: command.Description);

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Added {Points} points for {LoyaltyNumber}", command.Points, command.LoyaltyNumber);

        return transaction;
    }
}
