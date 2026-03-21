using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.DeleteAccount;

/// <summary>
/// Handles the <see cref="DeleteAccountCommand"/>.
/// Removes the user account and revokes all associated refresh tokens.
/// </summary>
public sealed class DeleteAccountHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<DeleteAccountHandler> _logger;

    public DeleteAccountHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<DeleteAccountHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        DeleteAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _userAccountRepository.GetByIdAsync(command.UserAccountId, cancellationToken);

        if (account is null)
        {
            _logger.LogDebug("Account not found for {UserAccountId}", command.UserAccountId);
            throw new KeyNotFoundException($"No user account found for ID '{command.UserAccountId}'.");
        }

        await _refreshTokenRepository.RevokeAllForUserAsync(command.UserAccountId, cancellationToken);
        await _userAccountRepository.DeleteAsync(command.UserAccountId, cancellationToken);

        _logger.LogInformation("Deleted user account {UserAccountId}", command.UserAccountId);
    }
}
