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

        // TODO: Look up the authorisation hold by redemptionReference to get the held points amount and release them.
        // For now, create the reversal record.
        var description = string.IsNullOrEmpty(command.Reason)
            ? $"Points reversal — {command.RedemptionReference}"
            : $"Points reversal — {command.RedemptionReference} — {command.Reason}";

        var transaction = LoyaltyTransaction.Create(
            loyaltyNumber: command.LoyaltyNumber,
            transactionType: "Adjustment",
            pointsDelta: 0,
            balanceAfter: customer.PointsBalance,
            description: description);

        _logger.LogInformation("Reversed redemption {RedemptionReference} for {LoyaltyNumber}", command.RedemptionReference, command.LoyaltyNumber);

        return transaction;
    }
}
