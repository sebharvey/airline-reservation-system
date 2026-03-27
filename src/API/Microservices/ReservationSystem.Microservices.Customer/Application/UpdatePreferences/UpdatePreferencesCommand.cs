namespace ReservationSystem.Microservices.Customer.Application.UpdatePreferences;

public sealed record UpdatePreferencesCommand(
    string LoyaltyNumber,
    bool MarketingEnabled,
    bool AnalyticsEnabled,
    bool FunctionalEnabled,
    bool AppNotificationsEnabled);
