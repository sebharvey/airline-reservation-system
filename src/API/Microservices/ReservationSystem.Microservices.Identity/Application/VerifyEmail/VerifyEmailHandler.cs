using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.VerifyEmail;

/// <summary>
/// Handles the <see cref="VerifyEmailCommand"/>.
/// Marks the user account's email as verified.
/// </summary>
public sealed class VerifyEmailHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<VerifyEmailHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
