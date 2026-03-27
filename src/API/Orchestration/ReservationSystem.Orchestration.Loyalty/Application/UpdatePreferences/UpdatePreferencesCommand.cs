namespace ReservationSystem.Orchestration.Loyalty.Application.UpdatePreferences;

public sealed record UpdatePreferencesCommand(
    string LoyaltyNumber,
    bool MarketingEnabled,
    bool AnalyticsEnabled,
    bool FunctionalEnabled,
    bool AppNotificationsEnabled);
