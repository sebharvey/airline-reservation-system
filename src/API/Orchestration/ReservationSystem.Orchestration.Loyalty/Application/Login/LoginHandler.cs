using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.Login;

public sealed class LoginHandler
{
    private readonly IdentityServiceClient _identityServiceClient;

    public LoginHandler(IdentityServiceClient identityServiceClient)
    {
        _identityServiceClient = identityServiceClient;
    }

    public Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
