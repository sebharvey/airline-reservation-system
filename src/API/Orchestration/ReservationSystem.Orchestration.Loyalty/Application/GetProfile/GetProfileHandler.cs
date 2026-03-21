using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.GetProfile;

public sealed class GetProfileHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public GetProfileHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public Task<ProfileResponse?> HandleAsync(GetProfileQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
