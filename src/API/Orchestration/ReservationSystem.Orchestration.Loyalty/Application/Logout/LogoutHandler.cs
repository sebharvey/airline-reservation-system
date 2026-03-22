using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.Logout;

/// <summary>
/// Orchestrates logout by delegating refresh token revocation to the Identity microservice.
/// </summary>
public sealed class LogoutHandler
{
    private readonly IdentityServiceClient _identityServiceClient;

    public LogoutHandler(IdentityServiceClient identityServiceClient)
    {
        _identityServiceClient = identityServiceClient;
    }

    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        await _identityServiceClient.LogoutAsync(command.RefreshToken, cancellationToken);
    }
}
