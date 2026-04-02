using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Models.Responses;
using ReservationSystem.Shared.Business.Security;

namespace ReservationSystem.Microservices.User.Application.AddUser;

/// <summary>
/// Handles the <see cref="AddUserCommand"/>.
/// Creates and persists a new employee user account.
/// </summary>
public sealed class AddUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AddUserHandler> _logger;

    public AddUserHandler(IUserRepository userRepository, ILogger<AddUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<AddUserResponse> HandleAsync(
        AddUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var existingByUsername = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);
        if (existingByUsername is not null)
        {
            _logger.LogDebug("AddUser failed: username '{Username}' already exists", command.Username);
            throw new InvalidOperationException("A user with this username already exists.");
        }

        var existingByEmail = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingByEmail is not null)
        {
            _logger.LogDebug("AddUser failed: email '{Email}' already registered", command.Email);
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var passwordHash = PasswordHasher.HashPassword(command.Password);
        var user = Domain.Entities.User.Create(command.Username, command.Email, passwordHash, command.FirstName, command.LastName);

        await _userRepository.CreateAsync(user, cancellationToken);

        _logger.LogInformation("Created user account {UserId} for username '{Username}'", user.UserId, user.Username);

        return new AddUserResponse { UserId = user.UserId };
    }


}
