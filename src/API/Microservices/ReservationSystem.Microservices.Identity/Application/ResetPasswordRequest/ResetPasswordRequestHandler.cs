using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;

/// <summary>
/// Handles the <see cref="ResetPasswordRequestCommand"/>.
/// Initiates the password reset flow by generating and sending a reset token.
/// Always completes successfully regardless of whether the email exists (enumeration protection).
/// </summary>
public sealed class ResetPasswordRequestHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<ResetPasswordRequestHandler> _logger;

    public ResetPasswordRequestHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<ResetPasswordRequestHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        ResetPasswordRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _userAccountRepository.GetByEmailAsync(command.Email, cancellationToken);

        if (account is null)
        {
            _logger.LogDebug("Password reset requested for unknown email — returning silently for enumeration protection");
            return;
        }

        var token = Guid.NewGuid();
        account.SetPasswordResetToken(token);
        await _userAccountRepository.UpdateAsync(account, cancellationToken);

        // Token delivery (email with reset link) is handled out of band.
        _logger.LogInformation("Password reset token generated for {UserAccountId}: {Token}", account.UserAccountId, token);
    }
}
