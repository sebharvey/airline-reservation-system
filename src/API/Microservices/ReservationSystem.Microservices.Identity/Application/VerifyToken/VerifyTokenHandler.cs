using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Shared.Business.Infrastructure.Configuration;
using ReservationSystem.Microservices.Identity.Models.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ReservationSystem.Microservices.Identity.Application.VerifyToken;

/// <summary>
/// Handles the <see cref="VerifyTokenCommand"/>.
/// Validates the JWT signature and expiry, then confirms the account is still active.
/// Called by the Loyalty orchestration layer before every protected request.
/// </summary>
public sealed class VerifyTokenHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<VerifyTokenHandler> _logger;

    public VerifyTokenHandler(
        IUserAccountRepository userAccountRepository,
        IOptions<JwtOptions> jwtOptions,
        ILogger<VerifyTokenHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<VerifyTokenResponse> HandleAsync(
        VerifyTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal principal;

        try
        {
            var keyBytes = Convert.FromBase64String(_jwtOptions.Secret);
            var key = new SymmetricSecurityKey(keyBytes);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = !string.IsNullOrWhiteSpace(_jwtOptions.Issuer),
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(_jwtOptions.Audience),
                ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(
                command.AccessToken, validationParameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogDebug("Token validation failed: {Message}", ex.Message);
            throw new UnauthorizedAccessException("Invalid or expired access token.");
        }

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sub, out var userAccountId))
        {
            _logger.LogDebug("Token 'sub' claim is not a valid GUID");
            throw new UnauthorizedAccessException("Invalid or expired access token.");
        }

        var account = await _userAccountRepository.GetByIdAsync(userAccountId, cancellationToken);

        if (account is null || account.IsLocked)
        {
            _logger.LogDebug("Account {UserAccountId} not found or locked during token verify", userAccountId);
            throw new UnauthorizedAccessException("Invalid or expired access token.");
        }

        return new VerifyTokenResponse
        {
            Valid = true,
            UserAccountId = account.UserAccountId,
            Email = account.Email
        };
    }
}
