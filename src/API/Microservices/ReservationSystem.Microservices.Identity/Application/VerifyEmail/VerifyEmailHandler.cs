using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.VerifyEmail;

/// <summary>
/// Handles the <see cref="VerifyEmailCommand"/>.
/// Marks the user account's email as verified.
/// </summary>
public sealed class VerifyEmailHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<VerifyEmailHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _userAccountRepository.GetByIdAsync(command.UserAccountId, cancellationToken);

        if (account is null)
        {
            _logger.LogDebug("Account not found for {UserAccountId}", command.UserAccountId);
            throw new KeyNotFoundException($"No user account found for ID '{command.UserAccountId}'.");
        }

        account.VerifyEmail();
        await _userAccountRepository.UpdateAsync(account, cancellationToken);

        _logger.LogInformation("Email verified for {UserAccountId}", account.UserAccountId);
    }
}
