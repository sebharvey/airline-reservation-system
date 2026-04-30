using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetAllUsers;

public sealed class GetAllUsersHandler
{
    private readonly UserServiceClient _userServiceClient;

    public GetAllUsersHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<IReadOnlyList<UserResponse>> HandleAsync(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await _userServiceClient.GetAllUsersAsync(cancellationToken);

        return users.Select(u => new UserResponse
        {
            UserId      = u.UserId,
            Username    = u.Username,
            Email       = u.Email,
            FirstName   = u.FirstName,
            LastName    = u.LastName,
            IsActive    = u.IsActive,
            IsLocked    = u.IsLocked,
            LastLoginAt = u.LastLoginAt,
            CreatedAt   = u.CreatedAt
        }).ToList();
    }
}
