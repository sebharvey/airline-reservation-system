using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;

/// <summary>
/// Handles the <see cref="ResetPasswordRequestCommand"/>.
/// Initiates the password reset flow by generating and sending a reset token.
/// </summary>
public sealed class ResetPasswordRequestHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<ResetPasswordRequestHandler> _logger;

    public ResetPasswordRequestHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<ResetPasswordRequestHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        ResetPasswordRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
