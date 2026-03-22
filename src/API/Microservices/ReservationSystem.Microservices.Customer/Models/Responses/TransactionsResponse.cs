namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body representing a loyalty transaction item.
/// </summary>
public sealed class TransactionResponse
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

/// <summary>
/// HTTP response body wrapping a paginated list of loyalty transactions.
/// </summary>
public sealed class TransactionsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<TransactionResponse> Transactions { get; init; } = [];
}
