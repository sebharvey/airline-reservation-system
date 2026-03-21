using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

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

    public Task HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
