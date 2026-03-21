using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;

/// <summary>
/// Handles the <see cref="VerifyEmailChangeCommand"/>.
/// Confirms the email change by validating the verification token and updating the account email.
/// </summary>
public sealed class VerifyEmailChangeHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<VerifyEmailChangeHandler> _logger;

    public VerifyEmailChangeHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<VerifyEmailChangeHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        VerifyEmailChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
