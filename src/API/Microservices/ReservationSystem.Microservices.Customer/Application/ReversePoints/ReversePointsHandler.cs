using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public async Task<LoyaltyTransaction?> HandleAsync(ReversePointsCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return null;
        }

        var hold = await _transactionRepository.FindAuthorisationHoldAsync(command.LoyaltyNumber, command.RedemptionReference, cancellationToken);

        if (hold is null)
        {
            _logger.LogDebug("No authorisation hold found for RedemptionReference {RedemptionReference}", command.RedemptionReference);
            return null;
        }

        var pointsToRestore = Math.Abs(hold.PointsDelta);

        customer.AddPoints(pointsToRestore);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        var description = string.IsNullOrEmpty(command.Reason)
            ? $"Points reversal — {command.RedemptionReference}"
            : $"Points reversal — {command.RedemptionReference} — {command.Reason}";

        var transaction = LoyaltyTransaction.Create(
            customerId: customer.CustomerId,
            transactionType: "Adjustment",
            pointsDelta: pointsToRestore,
            balanceAfter: customer.PointsBalance,
            description: description);

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Reversed {Points} points for redemption {RedemptionReference} on {LoyaltyNumber}", pointsToRestore, command.RedemptionReference, command.LoyaltyNumber);

        return transaction;
    }
}
