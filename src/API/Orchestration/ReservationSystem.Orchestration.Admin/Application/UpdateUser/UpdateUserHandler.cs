using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.UpdateUser;

public sealed class UpdateUserHandler
{
    private readonly UserServiceClient _userServiceClient;

    public UpdateUserHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<bool> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var body = new
        {
            firstName = command.FirstName,
            lastName  = command.LastName,
            email     = command.Email
        };

        return await _userServiceClient.UpdateUserAsync(command.UserId, body, cancellationToken);
    }
}
