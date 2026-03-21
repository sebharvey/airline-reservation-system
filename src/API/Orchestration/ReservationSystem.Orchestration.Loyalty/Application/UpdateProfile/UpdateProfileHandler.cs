using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;

public sealed class UpdateProfileHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public UpdateProfileHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public Task HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
