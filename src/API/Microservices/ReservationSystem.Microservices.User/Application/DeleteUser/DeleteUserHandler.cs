using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;

namespace ReservationSystem.Microservices.User.Application.DeleteUser;

/// <summary>
/// Handles the <see cref="DeleteUserCommand"/>.
/// Permanently removes an employee user account from the database.
/// </summary>
public sealed class DeleteUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DeleteUserHandler> _logger;

    public DeleteUserHandler(IUserRepository userRepository, ILogger<DeleteUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DeleteUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _userRepository.DeleteAsync(command.UserId, cancellationToken);

        if (!deleted)
        {
            _logger.LogDebug("DeleteUser: user not found for UserId {UserId}", command.UserId);
            return false;
        }

        _logger.LogInformation("Deleted user account {UserId}", command.UserId);
        return true;
    }
}
