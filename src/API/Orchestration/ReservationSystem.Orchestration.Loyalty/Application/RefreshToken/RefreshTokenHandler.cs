using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.RefreshToken;

/// <summary>
/// Orchestrates a token refresh by delegating to the Identity microservice.
/// The refresh token is single-use: Identity rotates it and returns a new pair.
/// </summary>
public sealed class RefreshTokenHandler
{
    private readonly IdentityServiceClient _identityServiceClient;

    public RefreshTokenHandler(IdentityServiceClient identityServiceClient)
    {
        _identityServiceClient = identityServiceClient;
    }

    public async Task<RefreshTokenResponse> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _identityServiceClient.RefreshTokenAsync(command.RefreshToken, cancellationToken);

        return new RefreshTokenResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresAt = result.ExpiresAt,
            TokenType = "Bearer"
        };
    }
}
