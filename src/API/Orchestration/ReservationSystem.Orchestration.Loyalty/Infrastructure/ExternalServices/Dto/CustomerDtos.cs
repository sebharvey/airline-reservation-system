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
    public string? Nationality { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int TierProgressPoints { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
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
