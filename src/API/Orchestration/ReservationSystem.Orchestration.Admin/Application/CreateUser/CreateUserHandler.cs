using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.CreateUser;

public sealed class CreateUserHandler
{
    private readonly UserServiceClient _userServiceClient;

    public CreateUserHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<AddUserResponse> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var body = new
        {
            username  = command.Username,
            email     = command.Email,
            password  = command.Password,
            firstName = command.FirstName,
            lastName  = command.LastName
        };

        var result = await _userServiceClient.CreateUserAsync(body, cancellationToken);

        return new AddUserResponse { UserId = result.UserId };
    }
}
