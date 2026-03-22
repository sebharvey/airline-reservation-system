namespace ReservationSystem.Microservices.Customer.Application.AddPoints;

/// <summary>
/// Command carrying the data needed to add points to a Customer's loyalty balance.
/// </summary>
public sealed record AddPointsCommand(
    string LoyaltyNumber,
    int Points,
    string TransactionType,
    string Description);
