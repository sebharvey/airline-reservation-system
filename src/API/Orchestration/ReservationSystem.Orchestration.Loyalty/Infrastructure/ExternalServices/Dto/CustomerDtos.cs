namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising Customer microservice responses.
// These are not exposed beyond the infrastructure layer.

public sealed class CustomerDto
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public Guid? IdentityId { get; init; }
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
    public string? PassportNumber { get; init; }
    public DateOnly? PassportIssueDate { get; init; }
    public string? PassportIssuer { get; init; }
    public DateOnly? PassportExpiryDate { get; init; }
    public string? KnownTravellerNumber { get; init; }
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int TierProgressPoints { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class CustomerPreferencesDto
{
    public Guid CustomerId { get; init; }
    public bool MarketingEnabled { get; init; }
    public bool AnalyticsEnabled { get; init; }
    public bool FunctionalEnabled { get; init; }
    public bool AppNotificationsEnabled { get; init; }
}

public sealed class CreateCustomerDto
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
}

public sealed class TransactionDto
{
    public Guid TransactionId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public int PointsDelta { get; init; }
    public int BalanceAfter { get; init; }
    public string? BookingReference { get; init; }
    public string? FlightNumber { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
}

public sealed class TransactionsDto
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<TransactionDto> Transactions { get; init; } = [];
}

public sealed class TransferPointsResultDto
{
    public string SenderLoyaltyNumber { get; init; } = string.Empty;
    public string RecipientLoyaltyNumber { get; init; } = string.Empty;
    public int PointsTransferred { get; init; }
    public int SenderNewBalance { get; init; }
    public int RecipientNewBalance { get; init; }
    public Guid SenderTransactionId { get; init; }
    public Guid RecipientTransactionId { get; init; }
    public DateTime TransferredAt { get; init; }
}
