using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;

/// <summary>
/// Handles the <see cref="EmailChangeRequestCommand"/>.
/// Initiates an email change by generating and sending a verification token to the new address.
/// </summary>
public sealed class EmailChangeRequestHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<EmailChangeRequestHandler> _logger;

    public EmailChangeRequestHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<EmailChangeRequestHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task HandleAsync(
        EmailChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
