namespace ReservationSystem.Microservices.Customer.Application.AuthorisePoints;

/// <summary>
/// Command carrying the data needed to authorise a points redemption hold for a Customer.
/// </summary>
public sealed record AuthorisePointsCommand(
    string LoyaltyNumber,
    int Points,
    Guid BasketId);
