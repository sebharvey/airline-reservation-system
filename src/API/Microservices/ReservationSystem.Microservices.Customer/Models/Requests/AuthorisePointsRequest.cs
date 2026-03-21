namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for authorising a points redemption hold.
/// </summary>
public sealed class AuthorisePointsRequest
{
    public int Points { get; init; }
    public Guid BasketId { get; init; }
}
