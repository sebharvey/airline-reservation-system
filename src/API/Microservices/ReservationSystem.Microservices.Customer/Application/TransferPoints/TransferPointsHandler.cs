using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;
using ReservationSystem.Microservices.Customer.Models.Responses;

namespace ReservationSystem.Microservices.Customer.Application.TransferPoints;

/// <summary>
/// Handles the <see cref="TransferPointsCommand"/>.
/// Debits points from the sender and credits them to the recipient,
/// recording an Adjustment transaction on each account.
/// </summary>
public sealed class TransferPointsHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyTransactionRepository _transactionRepository;
    private readonly ILogger<TransferPointsHandler> _logger;

    public TransferPointsHandler(
        ICustomerRepository customerRepository,
        ILoyaltyTransactionRepository transactionRepository,
        ILogger<TransferPointsHandler> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns <c>null</c> if either account is not found.
    /// Throws <see cref="InvalidOperationException"/> if the sender has insufficient points.
    /// </summary>
    public async Task<TransferPointsResponse?> HandleAsync(
        TransferPointsCommand command,
        CancellationToken cancellationToken = default)
    {
        var sender = await _customerRepository.GetByLoyaltyNumberAsync(
            command.SenderLoyaltyNumber, cancellationToken);

        if (sender is null)
        {
            _logger.LogDebug("Sender not found for LoyaltyNumber {LoyaltyNumber}", command.SenderLoyaltyNumber);
            return null;
        }

        var recipient = await _customerRepository.GetByLoyaltyNumberAsync(
            command.RecipientLoyaltyNumber, cancellationToken);

        if (recipient is null)
        {
            _logger.LogDebug("Recipient not found for LoyaltyNumber {LoyaltyNumber}", command.RecipientLoyaltyNumber);
            return null;
        }

        if (sender.PointsBalance < command.Points)
            throw new InvalidOperationException(
                $"Insufficient points balance. Available: {sender.PointsBalance}, requested: {command.Points}.");

        // Debit sender
        sender.DeductPoints(command.Points);
        await _customerRepository.UpdateAsync(sender, cancellationToken);

        var senderTransaction = LoyaltyTransaction.Create(
            customerId: sender.CustomerId,
            transactionType: "Adjustment",
            pointsDelta: -command.Points,
            balanceAfter: sender.PointsBalance,
            description: $"Points transferred to {command.RecipientLoyaltyNumber}");

        await _transactionRepository.CreateAsync(senderTransaction, cancellationToken);

        // Credit recipient
        recipient.AddPoints(command.Points);
        await _customerRepository.UpdateAsync(recipient, cancellationToken);

        var recipientTransaction = LoyaltyTransaction.Create(
            customerId: recipient.CustomerId,
            transactionType: "Adjustment",
            pointsDelta: command.Points,
            balanceAfter: recipient.PointsBalance,
            description: $"Points received from {command.SenderLoyaltyNumber}");

        await _transactionRepository.CreateAsync(recipientTransaction, cancellationToken);

        _logger.LogInformation(
            "Transferred {Points} points from {Sender} to {Recipient}",
            command.Points, command.SenderLoyaltyNumber, command.RecipientLoyaltyNumber);

        return new TransferPointsResponse
        {
            SenderLoyaltyNumber = command.SenderLoyaltyNumber,
            RecipientLoyaltyNumber = command.RecipientLoyaltyNumber,
            PointsTransferred = command.Points,
            SenderNewBalance = sender.PointsBalance,
            RecipientNewBalance = recipient.PointsBalance,
            SenderTransactionId = senderTransaction.TransactionId,
            RecipientTransactionId = recipientTransaction.TransactionId,
            TransferredAt = senderTransaction.TransactionDate
        };
    }
}
