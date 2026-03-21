using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;

namespace ReservationSystem.Microservices.Identity.Application.RefreshToken;

/// <summary>
/// Handles the <see cref="RefreshTokenCommand"/>.
/// Validates the refresh token and issues a new access token.
/// </summary>
public sealed class RefreshTokenHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<RefreshTokenHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public Task<RefreshTokenResponse> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
