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

        customer.DeductPoints(command.Points);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        var transaction = LoyaltyTransaction.Create(
            loyaltyNumber: command.LoyaltyNumber,
            transactionType: "Redeem",
            pointsDelta: -command.Points,
            balanceAfter: customer.PointsBalance,
            description: command.Description,
            bookingReference: command.BookingReference,
            flightNumber: command.FlightNumber);

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Settled {Points} points for {LoyaltyNumber}", command.Points, command.LoyaltyNumber);

        return transaction;
    }
}
