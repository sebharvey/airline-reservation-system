using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Shared.Business.Security;

namespace ReservationSystem.Microservices.Identity.Application.Logout;

/// <summary>
/// Handles the <see cref="LogoutCommand"/>.
/// Revokes the user's refresh token to invalidate their session.
/// </summary>
public sealed class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<LogoutHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = PasswordHasher.HashToken(command.RefreshToken);
        var token = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (token is null || token.IsRevoked)
        {
            _logger.LogDebug("Logout: refresh token not found or already revoked");
            return;
        }

        token.Revoke();
        await _refreshTokenRepository.UpdateAsync(token, cancellationToken);

        _logger.LogInformation("Logged out: revoked refresh token {RefreshTokenId}", token.RefreshTokenId);
    }
}
