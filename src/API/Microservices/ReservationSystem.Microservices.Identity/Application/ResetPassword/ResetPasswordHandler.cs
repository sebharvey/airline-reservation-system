using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.ResetPassword;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/>.
/// Validates the reset token and updates the user's password.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<ResetPasswordHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
