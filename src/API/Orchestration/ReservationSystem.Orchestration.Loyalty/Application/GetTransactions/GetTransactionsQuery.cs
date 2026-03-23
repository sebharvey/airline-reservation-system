namespace ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;

public sealed record GetTransactionsQuery(string LoyaltyNumber, int Page, int PageSize);
