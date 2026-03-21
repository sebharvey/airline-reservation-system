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

        customer.AddPoints(command.Points);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        var transaction = LoyaltyTransaction.Create(
            loyaltyNumber: command.LoyaltyNumber,
            transactionType: "Adjustment",
            pointsDelta: command.Points,
            balanceAfter: customer.PointsBalance,
            description: command.Description,
            bookingReference: command.BookingReference,
            flightNumber: command.FlightNumber);

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        _logger.LogInformation("Reversed {Points} points for {LoyaltyNumber}", command.Points, command.LoyaltyNumber);

        return transaction;
    }
}
