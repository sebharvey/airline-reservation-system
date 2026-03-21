using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;

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

    public LoginHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<LoginHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
