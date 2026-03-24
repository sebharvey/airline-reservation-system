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

    public async Task<ProfileResponse?> HandleAsync(GetProfileQuery query, CancellationToken cancellationToken)
    {
        var customer = await _customerServiceClient.GetCustomerAsync(query.LoyaltyNumber, cancellationToken);

        if (customer is null)
            return null;

        return new ProfileResponse
        {
            LoyaltyNumber = customer.LoyaltyNumber,
            GivenName = customer.GivenName,
            Surname = customer.Surname,
            Email = query.UserEmail,
            PhoneNumber = customer.PhoneNumber,
            DateOfBirth = customer.DateOfBirth,
            Tier = customer.TierCode,
            PointsBalance = customer.PointsBalance,
            MemberSince = customer.CreatedAt
        };
    }
}
