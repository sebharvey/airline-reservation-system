using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.EmailChangeRequest;

public sealed class EmailChangeRequestHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<EmailChangeRequestHandler> _logger;

    public EmailChangeRequestHandler(
        IdentityServiceClient identityServiceClient,
        ILogger<EmailChangeRequestHandler> logger)
    {
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    public async Task HandleAsync(EmailChangeRequestCommand command, CancellationToken cancellationToken = default)
    {
        await _identityServiceClient.EmailChangeRequestAsync(command.UserAccountId, command.NewEmail, cancellationToken);
        _logger.LogInformation("Email change request forwarded for {UserAccountId}", command.UserAccountId);
    }
}
