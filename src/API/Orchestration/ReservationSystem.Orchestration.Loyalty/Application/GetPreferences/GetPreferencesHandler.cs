using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Loyalty.Application.GetPreferences;

public sealed class GetPreferencesHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public GetPreferencesHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public async Task<CustomerPreferencesDto?> HandleAsync(GetPreferencesQuery query, CancellationToken cancellationToken)
    {
        return await _customerServiceClient.GetPreferencesAsync(query.LoyaltyNumber, cancellationToken);
    }
}
