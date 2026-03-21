namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for reinstating points to a customer's balance.
/// </summary>
public sealed class ReinstatePointsRequest
{
    public int Points { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
