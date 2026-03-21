namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for settling a previously authorised points redemption.
/// </summary>
public sealed class SettlePointsRequest
{
    public string RedemptionReference { get; init; } = string.Empty;
}
