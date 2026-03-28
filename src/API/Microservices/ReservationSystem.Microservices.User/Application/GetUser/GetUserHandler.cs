using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Models.Responses;

namespace ReservationSystem.Microservices.User.Application.GetUser;

/// <summary>
/// Handles the <see cref="GetUserQuery"/>.
/// Returns a single employee user account; password is never included.
/// </summary>
public sealed class GetUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserHandler> _logger;

    public GetUserHandler(IUserRepository userRepository, ILogger<GetUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UserResponse?> HandleAsync(
        GetUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("User not found for UserId {UserId}", query.UserId);
            return null;
        }

        return new UserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            IsLocked = user.IsLocked,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }
}
