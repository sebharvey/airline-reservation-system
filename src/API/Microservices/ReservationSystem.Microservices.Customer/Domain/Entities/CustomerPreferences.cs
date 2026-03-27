namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Stores communication and functional preference flags for a loyalty customer.
/// One-to-one with Customer (one row per CustomerId).
/// </summary>
public sealed class CustomerPreferences
{
    public Guid PreferenceId { get; private set; }
    public Guid CustomerId { get; private set; }
    public bool MarketingEnabled { get; private set; }
    public bool AnalyticsEnabled { get; private set; }
    public bool FunctionalEnabled { get; private set; }
    public bool AppNotificationsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CustomerPreferences() { }

    public void Update(
        bool? marketingEnabled = null,
        bool? analyticsEnabled = null,
        bool? functionalEnabled = null,
        bool? appNotificationsEnabled = null)
    {
        if (marketingEnabled.HasValue) MarketingEnabled = marketingEnabled.Value;
        if (analyticsEnabled.HasValue) AnalyticsEnabled = analyticsEnabled.Value;
        if (functionalEnabled.HasValue) FunctionalEnabled = functionalEnabled.Value;
        if (appNotificationsEnabled.HasValue) AppNotificationsEnabled = appNotificationsEnabled.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public static CustomerPreferences Create(Guid customerId) =>
        new()
        {
            PreferenceId = Guid.NewGuid(),
            CustomerId = customerId,
            MarketingEnabled = false,
            AnalyticsEnabled = false,
            FunctionalEnabled = true,
            AppNotificationsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static CustomerPreferences Reconstitute(
        Guid preferenceId,
        Guid customerId,
        bool marketingEnabled,
        bool analyticsEnabled,
        bool functionalEnabled,
        bool appNotificationsEnabled,
        DateTime createdAt,
        DateTime updatedAt) =>
        new()
        {
            PreferenceId = preferenceId,
            CustomerId = customerId,
            MarketingEnabled = marketingEnabled,
            AnalyticsEnabled = analyticsEnabled,
            FunctionalEnabled = functionalEnabled,
            AppNotificationsEnabled = appNotificationsEnabled,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
