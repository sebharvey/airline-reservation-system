namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class PreferencesResponse
{
    public bool MarketingEnabled { get; init; }
    public bool AnalyticsEnabled { get; init; }
    public bool FunctionalEnabled { get; init; }
    public bool AppNotificationsEnabled { get; init; }
}
