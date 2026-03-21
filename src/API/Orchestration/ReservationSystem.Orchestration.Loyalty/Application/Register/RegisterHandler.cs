using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.Register;

public sealed class RegisterHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public RegisterHandler(
        IdentityServiceClient identityServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _identityServiceClient = identityServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public Task<ProfileResponse> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
