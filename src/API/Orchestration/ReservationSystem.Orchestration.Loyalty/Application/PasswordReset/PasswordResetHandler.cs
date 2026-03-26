using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.PasswordReset;

public sealed class PasswordResetHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<PasswordResetHandler> _logger;

    public PasswordResetHandler(
        IdentityServiceClient identityServiceClient,
        ILogger<PasswordResetHandler> logger)
    {
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    public async Task HandleAsync(PasswordResetCommand command, CancellationToken cancellationToken = default)
    {
        await _identityServiceClient.PasswordResetAsync(command.Token, command.NewPassword, cancellationToken);
        _logger.LogInformation("Password reset completed via token");
    }
}
