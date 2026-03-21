using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;
using System.Security.Cryptography;
using System.Text;

namespace ReservationSystem.Microservices.Identity.Application.Login;

/// <summary>
/// Handles the <see cref="LoginCommand"/>.
/// Validates credentials and issues access and refresh tokens.
/// </summary>
public sealed class LoginHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<LoginHandler> _logger;

    private const int MaxFailedAttempts = 5;
    private const int RefreshTokenDays = 30;

    public LoginHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<LoginHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
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

        if (account.IsLocked)
        {
            _logger.LogDebug("Login failed: account is locked for {UserAccountId}", account.UserAccountId);
            throw new InvalidOperationException("Account is locked due to repeated failed login attempts.");
        }

        var passwordValid = VerifyPassword(command.Password, account.PasswordHash);

        if (!passwordValid)
        {
            account.RecordFailedLogin();

            if (account.FailedLoginAttempts >= MaxFailedAttempts)
                account.Lock();

            await _userAccountRepository.UpdateAsync(account, cancellationToken);

            _logger.LogDebug("Login failed: invalid password for {UserAccountId}", account.UserAccountId);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        account.RecordSuccessfulLogin();
        await _userAccountRepository.UpdateAsync(account, cancellationToken);

        var rawRefreshToken = GenerateSecureToken();
        var refreshTokenHash = HashToken(rawRefreshToken);
        var refreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);

        var refreshToken = RefreshToken.Create(
            userAccountId: account.UserAccountId,
            tokenHash: refreshTokenHash,
            expiresAt: refreshTokenExpiry);

        await _refreshTokenRepository.CreateAsync(refreshToken, cancellationToken);

        var accessToken = GenerateAccessToken(account);

        _logger.LogInformation("Login succeeded for {UserAccountId}", account.UserAccountId);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            IdentityReference = account.IdentityReference
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

    private static string GenerateAccessToken(UserAccount account)
    {
        var payload = $"{account.UserAccountId}:{account.IdentityReference}:{account.Email}:{DateTimeOffset.UtcNow.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes);
    }
}
