using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;

namespace ReservationSystem.Microservices.User.Application.UnlockUser;

/// <summary>
/// Handles the <see cref="UnlockUserCommand"/>.
/// Unlocks a locked employee user account and resets failed login attempts.
/// </summary>
public sealed class UnlockUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UnlockUserHandler> _logger;

    public UnlockUserHandler(IUserRepository userRepository, ILogger<UnlockUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        UnlockUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("UnlockUser: user not found for UserId {UserId}", command.UserId);
            return false;
        }

        user.Unlock();
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Unlocked user account {UserId}", command.UserId);
        return true;
    }
}
