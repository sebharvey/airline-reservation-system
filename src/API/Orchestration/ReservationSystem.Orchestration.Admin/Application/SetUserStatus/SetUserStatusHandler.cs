using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.SetUserStatus;

public sealed class SetUserStatusHandler
{
    private readonly UserServiceClient _userServiceClient;

    public SetUserStatusHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<bool> HandleAsync(SetUserStatusCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsActive && command.UserId == command.StaffUserId)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        return await _userServiceClient.SetUserStatusAsync(command.UserId, command.IsActive, cancellationToken);
    }
}
