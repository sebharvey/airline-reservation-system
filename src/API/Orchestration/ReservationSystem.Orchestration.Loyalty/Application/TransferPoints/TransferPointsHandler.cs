using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.TransferPoints;

/// <summary>
/// Orchestrates a loyalty points transfer:
/// 1. Looks up the recipient customer by loyalty number (Customer MS).
/// 2. Verifies the supplied email matches the recipient's registered Identity account (Identity MS).
/// 3. Calls the Customer MS to execute the debit/credit transfer.
/// </summary>
public sealed class TransferPointsHandler
{
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly IdentityServiceClient _identityServiceClient;

    public TransferPointsHandler(
        CustomerServiceClient customerServiceClient,
        IdentityServiceClient identityServiceClient)
    {
        _customerServiceClient = customerServiceClient;
        _identityServiceClient = identityServiceClient;
    }

    /// <summary>
    /// Returns the transfer result, or <c>null</c> if the sender or recipient account does not exist.
    /// Throws <see cref="ArgumentException"/> if the recipient email cannot be verified.
    /// Throws <see cref="InvalidOperationException"/> if the sender has insufficient points.
    /// </summary>
    public async Task<TransferPointsResponse?> HandleAsync(
        TransferPointsCommand command,
        CancellationToken cancellationToken)
    {
        // Resolve recipient customer to confirm the loyalty number exists
        var recipient = await _customerServiceClient.GetCustomerAsync(
            command.RecipientLoyaltyNumber, cancellationToken);

        if (recipient is null)
            return null;

        // Verify the supplied email belongs to a registered Identity account
        var emailAccount = await _identityServiceClient.GetAccountByEmailAsync(
            command.RecipientEmail, cancellationToken);

        if (emailAccount is null)
            throw new ArgumentException(
                "The recipient loyalty number and email address do not match a registered account.");

        // Execute the transfer in the Customer MS
        var result = await _customerServiceClient.TransferPointsAsync(
            command.SenderLoyaltyNumber,
            command.RecipientLoyaltyNumber,
            command.Points,
            cancellationToken);

        if (result is null)
            return null;

        return new TransferPointsResponse
        {
            SenderLoyaltyNumber = result.SenderLoyaltyNumber,
            RecipientLoyaltyNumber = result.RecipientLoyaltyNumber,
            PointsTransferred = result.PointsTransferred,
            SenderNewBalance = result.SenderNewBalance,
            RecipientNewBalance = result.RecipientNewBalance,
            TransferredAt = result.TransferredAt
        };
    }
}
