using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;
using ReservationSystem.Shared.Business.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RefreshTokenEntity = ReservationSystem.Microservices.Identity.Domain.Entities.RefreshToken;

namespace ReservationSystem.Microservices.Identity.Application.Login;

/// <summary>
/// Handles the <see cref="LoginCommand"/>.
/// Validates credentials and issues a signed JWT access token plus a refresh token.
/// </summary>
public sealed class LoginHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LoginHandler> _logger;

    private const int MaxFailedAttempts = 5;
    private const int RefreshTokenDays = 30;

    public LoginHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtService jwtService,
        ILogger<LoginHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _userAccountRepository.GetByEmailAsync(command.Email, cancellationToken);

        if (account is null)
        {
            _logger.LogDebug("Login failed: account not found");
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var passwordValid = PasswordHasher.VerifyPassword(command.Password, account.PasswordHash);

        if (!passwordValid)
        {
            if (!account.IsLocked)
            {
                account.RecordFailedLogin();

                if (account.FailedLoginAttempts >= MaxFailedAttempts)
                    account.Lock();

                await _userAccountRepository.UpdateAsync(account, cancellationToken);
            }

            _logger.LogDebug("Login failed: invalid password for {UserAccountId}", account.UserAccountId);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (account.IsLocked)
        {
            _logger.LogDebug("Login failed: account is locked for {UserAccountId}", account.UserAccountId);
            throw new InvalidOperationException("Account is locked due to repeated failed login attempts.");
        }

        if (!account.IsEmailVerified)
        {
            _logger.LogDebug("Login failed: email not verified for {UserAccountId}", account.UserAccountId);
            throw new InvalidOperationException("Email address has not been verified. Please verify your email before logging in.");
        }

        account.RecordSuccessfulLogin();
        await _userAccountRepository.UpdateAsync(account, cancellationToken);

        var rawRefreshToken = PasswordHasher.GenerateSecureToken();
        var refreshTokenHash = PasswordHasher.HashToken(rawRefreshToken);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenDays);

        var refreshToken = RefreshTokenEntity.Create(
            userAccountId: account.UserAccountId,
            tokenHash: refreshTokenHash,
            expiresAt: refreshTokenExpiry);

        await _refreshTokenRepository.CreateAsync(refreshToken, cancellationToken);

        var (accessToken, expiresAt) = GenerateJwt(account);

        _logger.LogInformation("Login succeeded for {UserAccountId}", account.UserAccountId);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            UserAccountId = account.UserAccountId,
            ExpiresAt = expiresAt
        };
    }

    internal (string Token, DateTime ExpiresAt) GenerateJwt(UserAccount account)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.UserAccountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        return _jwtService.GenerateToken(claims);
    }
}
