using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.UnlockUser;

public sealed class UnlockUserHandler
{
    private readonly UserServiceClient _userServiceClient;

    public UnlockUserHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<bool> HandleAsync(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        return await _userServiceClient.UnlockUserAsync(command.UserId, cancellationToken);
    }
}
