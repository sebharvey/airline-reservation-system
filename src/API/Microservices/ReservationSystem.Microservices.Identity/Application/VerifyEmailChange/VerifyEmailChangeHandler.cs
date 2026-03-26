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
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<VerifyEmailChangeHandler> _logger;

    public VerifyEmailChangeHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<VerifyEmailChangeHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        VerifyEmailChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(command.Token, out var tokenGuid))
            throw new ArgumentException("Invalid verification token.");

        var account = await _userAccountRepository.GetByEmailResetTokenAsync(tokenGuid, cancellationToken);

        if (account is null)
            throw new ArgumentException("Invalid or expired verification token.");

        account.ChangeEmail(command.NewEmail);
        account.VerifyEmail();
        account.ClearEmailResetToken();

        await _userAccountRepository.UpdateAsync(account, cancellationToken);
        await _refreshTokenRepository.RevokeAllForUserAsync(account.UserAccountId, cancellationToken);

        _logger.LogInformation("Email change completed for {UserAccountId}", account.UserAccountId);
    }
}
