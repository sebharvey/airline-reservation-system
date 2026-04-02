using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Models.Responses;
using ReservationSystem.Shared.Business.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ReservationSystem.Microservices.User.Application.Login;

/// <summary>
/// Handles the <see cref="LoginCommand"/>.
/// Validates credentials and issues a signed JWT access token.
/// </summary>
public sealed class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LoginHandler> _logger;

    private const int MaxFailedAttempts = 5;

    public LoginHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
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

        var passwordValid = PasswordHasher.VerifyPassword(command.Password, user.PasswordHash);

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

    private (string Token, DateTime ExpiresAt) GenerateJwt(Domain.Entities.User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User"),
        };

        return _jwtService.GenerateToken(claims);
    }
}
