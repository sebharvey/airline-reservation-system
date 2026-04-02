using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Shared.Business.Security;

namespace ReservationSystem.Microservices.Identity.Application.ResetPassword;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/>.
/// Validates the reset token and updates the user's password.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<ResetPasswordHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(command.Token, out var tokenGuid))
            throw new ArgumentException("Invalid reset token.");

        var account = await _userAccountRepository.GetByPasswordResetTokenAsync(tokenGuid, cancellationToken);

        if (account is null)
            throw new ArgumentException("Invalid or expired reset token.");

        var newPasswordHash = PasswordHasher.HashPassword(command.NewPassword);
        account.ChangePassword(newPasswordHash);
        account.Unlock();
        account.ClearPasswordResetToken();

        await _userAccountRepository.UpdateAsync(account, cancellationToken);
        await _refreshTokenRepository.RevokeAllForUserAsync(account.UserAccountId, cancellationToken);

        _logger.LogInformation("Password reset completed for {UserAccountId}", account.UserAccountId);
    }
}
