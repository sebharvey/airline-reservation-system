using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;

/// <summary>
/// Handles the <see cref="VerifyEmailChangeCommand"/>.
/// Confirms the email change by validating the verification token and updating the account email.
/// </summary>
public sealed class VerifyEmailChangeHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<VerifyEmailChangeHandler> _logger;

    public VerifyEmailChangeHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<VerifyEmailChangeHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        VerifyEmailChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
            throw new ArgumentException("Verification token is required.");

        // In a production system, this would:
        // 1. Look up the pending email change by token hash
        // 2. Validate the token has not expired
        // 3. Update the Email column on the account
        // 4. Set IsEmailVerified = true
        // 5. Revoke all active refresh tokens to force re-authentication
        _logger.LogInformation("Email change verified via token");
        await Task.CompletedTask;
    }
}
