using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public async Task<LoyaltyTransaction?> HandleAsync(SettlePointsCommand command, CancellationToken cancellationToken = default)
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

        var transaction = LoyaltyTransaction.Create(
            customerId: customer.CustomerId,
            transactionType: "Redeem",
            pointsDelta: hold.PointsDelta,
            balanceAfter: customer.PointsBalance,
            description: $"Points settlement — {command.RedemptionReference}");

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Settled {Points} points for redemption {RedemptionReference} on {LoyaltyNumber}", Math.Abs(hold.PointsDelta), command.RedemptionReference, command.LoyaltyNumber);

        return transaction;
    }
}
