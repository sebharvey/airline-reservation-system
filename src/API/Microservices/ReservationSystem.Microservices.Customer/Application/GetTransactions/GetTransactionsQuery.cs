namespace ReservationSystem.Microservices.Customer.Application.GetTransactions;

/// <summary>
/// Query carrying the loyalty number and pagination parameters needed to retrieve transactions for a Customer.
/// </summary>
public sealed record GetTransactionsQuery(string LoyaltyNumber, int Page = 1, int PageSize = 20);
