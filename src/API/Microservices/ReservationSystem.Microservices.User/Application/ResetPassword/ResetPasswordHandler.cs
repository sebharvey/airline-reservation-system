using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Shared.Business.Security;

namespace ReservationSystem.Microservices.User.Application.ResetPassword;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/>.
/// Resets an employee user's password, unlocks the account, and clears failed attempts.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(IUserRepository userRepository, ILogger<ResetPasswordHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("ResetPassword: user not found for UserId {UserId}", command.UserId);
            return false;
        }

        var passwordHash = PasswordHasher.HashPassword(command.NewPassword);
        user.ResetPassword(passwordHash);
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Reset password for user account {UserId}", command.UserId);
        return true;
    }
}
