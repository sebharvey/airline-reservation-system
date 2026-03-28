using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.Login;

/// <summary>
/// Orchestrates a staff login request by delegating credential validation and
/// token issuance entirely to the User microservice.
/// </summary>
public sealed class LoginHandler
{
    private readonly UserServiceClient _userServiceClient;

    public LoginHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _userServiceClient.LoginAsync(command.Username, command.Password, cancellationToken);

        return new LoginResponse
        {
            AccessToken = result.AccessToken,
            UserId = result.UserId,
            ExpiresAt = result.ExpiresAt,
            TokenType = "Bearer"
        };
    }
}
