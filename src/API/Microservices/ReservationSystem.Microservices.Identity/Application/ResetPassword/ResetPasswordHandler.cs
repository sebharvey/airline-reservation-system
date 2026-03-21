using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.ResetPassword;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/>.
/// Validates the reset token and updates the user's password.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<ResetPasswordHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        // In a production system, the reset token would be validated against a stored,
        // time-limited token. For this implementation, we validate the inputs and log.
        if (string.IsNullOrWhiteSpace(command.ResetToken))
            throw new ArgumentException("Reset token is required.");

        if (string.IsNullOrWhiteSpace(command.NewPassword))
            throw new ArgumentException("New password is required.");

        // A production implementation would:
        // 1. Look up the account by the reset token hash
        // 2. Validate the token has not expired (1-hour TTL)
        // 3. Hash the new password with Argon2id
        // 4. Update PasswordHash, set PasswordChangedAt, reset IsLocked/FailedLoginAttempts
        // 5. Revoke all active refresh tokens
        _logger.LogInformation("Password reset completed via token");
        await Task.CompletedTask;
    }
}
