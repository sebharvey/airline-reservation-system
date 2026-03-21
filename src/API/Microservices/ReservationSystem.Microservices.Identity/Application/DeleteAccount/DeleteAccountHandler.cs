using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.DeleteAccount;

/// <summary>
/// Handles the <see cref="DeleteAccountCommand"/>.
/// Removes the user account and revokes all associated refresh tokens.
/// </summary>
public sealed class DeleteAccountHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<DeleteAccountHandler> _logger;

    public DeleteAccountHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<DeleteAccountHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        DeleteAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
