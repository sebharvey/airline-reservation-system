using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.VerifyEmailChange;

public sealed class VerifyEmailChangeHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<VerifyEmailChangeHandler> _logger;

    public VerifyEmailChangeHandler(
        IdentityServiceClient identityServiceClient,
        ILogger<VerifyEmailChangeHandler> logger)
    {
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    public async Task HandleAsync(VerifyEmailChangeCommand command, CancellationToken cancellationToken = default)
    {
        await _identityServiceClient.VerifyEmailChangeAsync(command.Token, command.NewEmail, cancellationToken);
        _logger.LogInformation("Email change verification completed via token");
    }
}
