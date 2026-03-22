using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;

/// <summary>
/// Handles the <see cref="EmailChangeRequestCommand"/>.
/// Initiates an email change by generating and sending a verification token to the new address.
/// </summary>
public sealed class EmailChangeRequestHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<EmailChangeRequestHandler> _logger;

    public EmailChangeRequestHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<EmailChangeRequestHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        EmailChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _userAccountRepository.GetByIdAsync(command.UserAccountId, cancellationToken);

        if (account is null)
        {
            _logger.LogDebug("Account not found for UserAccountId {UserAccountId}", command.UserAccountId);
            throw new KeyNotFoundException($"No user account found for ID '{command.UserAccountId}'.");
        }

        var existingWithEmail = await _userAccountRepository.GetByEmailAsync(command.NewEmail, cancellationToken);

        if (existingWithEmail is not null)
        {
            _logger.LogDebug("Email change request failed: new email already registered");
            throw new InvalidOperationException("The new email address is already registered to another account.");
        }

        // In a production system, this would generate a verification token,
        // persist it, and send a confirmation email to the new address.
        _logger.LogInformation("Email change request initiated for {UserAccountId} to new email", account.UserAccountId);
    }
}
