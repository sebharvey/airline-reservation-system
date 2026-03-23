namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

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

public sealed class TransactionsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<TransactionResponse> Transactions { get; init; } = [];
}
