using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Models.Responses;

namespace ReservationSystem.Microservices.User.Application.GetAllUsers;

/// <summary>
/// Handles the <see cref="GetAllUsersQuery"/>.
/// Returns all employee user accounts; passwords are never included in the response.
/// </summary>
public sealed class GetAllUsersHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetAllUsersHandler> _logger;

    public GetAllUsersHandler(IUserRepository userRepository, ILogger<GetAllUsersHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserResponse>> HandleAsync(
        GetAllUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} user accounts", users.Count);

        return users
            .Select(u => new UserResponse
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            })
            .ToList();
    }
}
