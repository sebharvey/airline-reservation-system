namespace ReservationSystem.Microservices.Customer.Application.GetTransactions;

/// <summary>
/// Query carrying the loyalty number needed to retrieve transactions for a Customer.
/// </summary>
public sealed record GetTransactionsQuery(string LoyaltyNumber);
