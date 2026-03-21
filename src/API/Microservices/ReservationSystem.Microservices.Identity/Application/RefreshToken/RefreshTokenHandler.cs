using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;
using System.Security.Cryptography;
using System.Text;

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

    private const int AccessTokenMinutes = 15;
    private const int RefreshTokenDays = 30;

    public RefreshTokenHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<RefreshTokenHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<RefreshTokenResponse> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = LoginHandler.HashToken(command.Token);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsValid)
        {
            _logger.LogDebug("Refresh token not found or invalid");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        existingToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(existingToken, cancellationToken);

        var account = await _userAccountRepository.GetByIdAsync(existingToken.UserAccountId, cancellationToken);

        if (account is null)
        {
            _logger.LogWarning("User account not found for refresh token {RefreshTokenId}", existingToken.RefreshTokenId);
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var rawNewToken = GenerateSecureToken();
        var newTokenHash = LoginHandler.HashToken(rawNewToken);
        var newExpiry = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);

        var newRefreshToken = Domain.Entities.RefreshToken.Create(
            userAccountId: account.UserAccountId,
            tokenHash: newTokenHash,
            expiresAt: newExpiry,
            deviceHint: command.DeviceHint);

        await _refreshTokenRepository.CreateAsync(newRefreshToken, cancellationToken);

        var accessToken = GenerateAccessToken(account);
        var accessTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(AccessTokenMinutes);

        _logger.LogInformation("Refresh token rotated for {UserAccountId}", account.UserAccountId);

        return new RefreshTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawNewToken,
            ExpiresAt = accessTokenExpiry
        };
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateAccessToken(Domain.Entities.UserAccount account)
    {
        var payload = $"{account.UserAccountId}:{account.IdentityReference}:{account.Email}:{DateTimeOffset.UtcNow.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes);
    }
}
