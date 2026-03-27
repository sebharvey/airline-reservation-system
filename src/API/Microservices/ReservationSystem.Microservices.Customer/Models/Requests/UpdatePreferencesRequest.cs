namespace ReservationSystem.Microservices.Customer.Models.Requests;

public sealed class UpdatePreferencesRequest
{
    public bool MarketingEnabled { get; init; }
    public bool AnalyticsEnabled { get; init; }
    public bool FunctionalEnabled { get; init; }
    public bool AppNotificationsEnabled { get; init; }
}
