using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Microservices.User.Domain.Entities;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Infrastructure.Configuration;
using ReservationSystem.Microservices.User.Models.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ReservationSystem.Microservices.User.Application.Login;

/// <summary>
/// Handles the <see cref="LoginCommand"/>.
/// Validates credentials and issues a signed JWT access token.
/// </summary>
public sealed class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<LoginHandler> _logger;

    private const int MaxFailedAttempts = 5;

    public LoginHandler(
        IUserRepository userRepository,
        IOptions<JwtOptions> jwtOptions,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);

        if (user is null)
        {
            _logger.LogDebug("Login failed: username not found");
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            _logger.LogDebug("Login failed: account inactive for {UserId}", user.UserId);
            throw new InvalidOperationException("Account is inactive.");
        }

        if (user.IsLocked)
        {
            _logger.LogDebug("Login failed: account locked for {UserId}", user.UserId);
            throw new InvalidOperationException("Account is locked due to repeated failed login attempts.");
        }

        var passwordValid = VerifyPassword(command.Password, user.PasswordHash);

        if (!passwordValid)
        {
            user.RecordFailedLogin();

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.Lock();

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogDebug("Login failed: invalid password for {UserId}", user.UserId);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        user.RecordSuccessfulLogin();
        await _userRepository.UpdateAsync(user, cancellationToken);

        var (accessToken, expiresAt) = GenerateJwt(user);

        _logger.LogInformation("Login succeeded for {UserId}", user.UserId);

        return new LoginResponse
        {
            AccessToken = accessToken,
            UserId = user.UserId,
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

    internal (string Token, DateTime ExpiresAt) GenerateJwt(User user)
    {
        var keyBytes = Convert.FromBase64String(_jwtOptions.Secret);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
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
