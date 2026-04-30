using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.ResetPassword;

public sealed class ResetPasswordHandler
{
    private readonly UserServiceClient _userServiceClient;

    public ResetPasswordHandler(UserServiceClient userServiceClient)
    {
        _userServiceClient = userServiceClient;
    }

    public async Task<bool> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        return await _userServiceClient.ResetPasswordAsync(command.UserId, command.NewPassword, cancellationToken);
    }
}
