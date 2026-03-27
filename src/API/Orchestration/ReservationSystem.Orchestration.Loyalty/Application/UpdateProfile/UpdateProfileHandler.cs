using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;

public sealed class UpdateProfileHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public UpdateProfileHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    /// <returns>True if updated; false if the customer was not found.</returns>
    public async Task<bool> HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var updateRequest = new
        {
            command.GivenName,
            command.Surname,
            command.DateOfBirth,
            command.Gender,
            command.Nationality,
            command.PhoneNumber,
            command.PreferredLanguage,
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.StateOrRegion,
            command.PostalCode,
            command.CountryCode
        };

        return await _customerServiceClient.UpdateCustomerAsync(
            command.LoyaltyNumber, updateRequest, cancellationToken);
    }
}
