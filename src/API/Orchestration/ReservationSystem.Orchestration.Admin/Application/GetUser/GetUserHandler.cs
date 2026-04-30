using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetUser;

public sealed class GetUserHandler
{
    private readonly UserServiceClient _userServiceClient;

    public GetUserHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<UserResponse?> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await _userServiceClient.GetUserAsync(query.UserId, cancellationToken);

        if (user is null)
            return null;

        return new UserResponse
        {
            UserId      = user.UserId,
            Username    = user.Username,
            Email       = user.Email,
            FirstName   = user.FirstName,
            LastName    = user.LastName,
            IsActive    = user.IsActive,
            IsLocked    = user.IsLocked,
            LastLoginAt = user.LastLoginAt,
            CreatedAt   = user.CreatedAt
        };
    }
}
