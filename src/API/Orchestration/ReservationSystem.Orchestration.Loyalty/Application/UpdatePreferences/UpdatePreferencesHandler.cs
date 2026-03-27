using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.UpdatePreferences;

public sealed class UpdatePreferencesHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public UpdatePreferencesHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    /// <returns>True if updated; false if the customer was not found.</returns>
    public async Task<bool> HandleAsync(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        var body = new
        {
            command.MarketingEnabled,
            command.AnalyticsEnabled,
            command.FunctionalEnabled,
            command.AppNotificationsEnabled
        };

        return await _customerServiceClient.UpdatePreferencesAsync(command.LoyaltyNumber, body, cancellationToken);
    }
}
