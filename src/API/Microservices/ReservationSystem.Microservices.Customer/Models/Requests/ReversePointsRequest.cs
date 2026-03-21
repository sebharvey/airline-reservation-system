namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for reversing a points authorisation hold.
/// </summary>
public sealed class ReversePointsRequest
{
    public string RedemptionReference { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
