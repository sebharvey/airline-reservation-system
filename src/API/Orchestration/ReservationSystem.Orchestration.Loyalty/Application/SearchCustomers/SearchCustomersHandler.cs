using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.SearchCustomers;

public sealed class SearchCustomersHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public SearchCustomersHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public async Task<IReadOnlyList<CustomerSummaryResponse>> HandleAsync(
        SearchCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var customers = await _customerServiceClient.SearchCustomersAsync(query.Query, cancellationToken);

        return customers.Select(c => new CustomerSummaryResponse
        {
            LoyaltyNumber = c.LoyaltyNumber,
            GivenName = c.GivenName,
            Surname = c.Surname,
            TierCode = c.TierCode,
            PointsBalance = c.PointsBalance,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
        }).ToList();
    }
}
