using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Infrastructure.Configuration;
using ReservationSystem.Microservices.Identity.Models.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<LoginHandler> _logger;

    private const int MaxFailedAttempts = 5;
    private const int RefreshTokenDays = 30;

    public LoginHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOptions<JwtOptions> jwtOptions,
        ILogger<LoginHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtOptions = jwtOptions.Value;
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

        var passwordValid = VerifyPassword(command.Password, account.PasswordHash);

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

        var rawRefreshToken = GenerateSecureToken();
        var refreshTokenHash = HashToken(rawRefreshToken);
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

    private static bool VerifyPassword(string plaintext, string storedHash)
    {
        var computedHash = HashPassword(plaintext);
        return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
    }

    internal static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    internal static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    internal (string Token, DateTime ExpiresAt) GenerateJwt(UserAccount account)
    {
        var keyBytes = Convert.FromBase64String(_jwtOptions.Secret);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.UserAccountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
