using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;

namespace ReservationSystem.Microservices.User.Application.SetUserStatus;

/// <summary>
/// Handles the <see cref="SetUserStatusCommand"/>.
/// Activates or deactivates an employee user account.
/// </summary>
public sealed class SetUserStatusHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SetUserStatusHandler> _logger;

    public SetUserStatusHandler(IUserRepository userRepository, ILogger<SetUserStatusHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        SetUserStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("SetUserStatus: user not found for UserId {UserId}", command.UserId);
            return false;
        }

        user.SetActive(command.IsActive);
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Set user {UserId} active status to {IsActive}", command.UserId, command.IsActive);
        return true;
    }
}
