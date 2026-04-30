using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.DeleteUser;

public sealed class DeleteUserHandler
{
    private readonly UserServiceClient _userServiceClient;

    public DeleteUserHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<bool> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == command.StaffUserId)
            throw new InvalidOperationException("You cannot delete your own account.");

        return await _userServiceClient.DeleteUserAsync(command.UserId, cancellationToken);
    }
}
