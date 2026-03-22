namespace ReservationSystem.Microservices.Customer.Models.Responses;

/// <summary>
/// HTTP response body returned after authorising a points redemption hold.
/// </summary>
public sealed class AuthorisePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsAuthorised { get; init; }
    public int PointsHeld { get; init; }
    public DateTime AuthorisedAt { get; init; }
}
