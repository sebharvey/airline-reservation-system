namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for reversing points on a customer loyalty account.
/// </summary>
public sealed class ReversePointsRequest
{
    public int Points { get; init; }
    public string? BookingReference { get; init; }
    public string? FlightNumber { get; init; }
    public string Description { get; init; } = string.Empty;
}
