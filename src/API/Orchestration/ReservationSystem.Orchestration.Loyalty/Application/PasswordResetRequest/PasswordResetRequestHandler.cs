using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.PasswordResetRequest;

public sealed class PasswordResetRequestHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<PasswordResetRequestHandler> _logger;

    public PasswordResetRequestHandler(
        IdentityServiceClient identityServiceClient,
        ILogger<PasswordResetRequestHandler> logger)
    {
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    public async Task HandleAsync(PasswordResetRequestCommand command, CancellationToken cancellationToken = default)
    {
        await _identityServiceClient.PasswordResetRequestAsync(command.Email, cancellationToken);
        _logger.LogDebug("Password reset request forwarded for email");
    }
}
