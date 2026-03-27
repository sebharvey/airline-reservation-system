namespace ReservationSystem.Microservices.Customer.Models.Responses;

public sealed class CustomerPreferencesResponse
{
    public Guid CustomerId { get; init; }
    public bool MarketingEnabled { get; init; }
    public bool AnalyticsEnabled { get; init; }
    public bool FunctionalEnabled { get; init; }
    public bool AppNotificationsEnabled { get; init; }
}
