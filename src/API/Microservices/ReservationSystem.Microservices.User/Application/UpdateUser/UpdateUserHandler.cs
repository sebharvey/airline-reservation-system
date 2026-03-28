using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;

namespace ReservationSystem.Microservices.User.Application.UpdateUser;

/// <summary>
/// Handles the <see cref="UpdateUserCommand"/>.
/// Updates profile fields on an existing employee user account.
/// </summary>
public sealed class UpdateUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(IUserRepository userRepository, ILogger<UpdateUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("UpdateUser: user not found for UserId {UserId}", command.UserId);
            return false;
        }

        if (command.Email is not null)
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (existingByEmail is not null && existingByEmail.UserId != command.UserId)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }
        }

        user.UpdateProfile(command.FirstName, command.LastName, command.Email);
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Updated user account {UserId}", command.UserId);
        return true;
    }
}
