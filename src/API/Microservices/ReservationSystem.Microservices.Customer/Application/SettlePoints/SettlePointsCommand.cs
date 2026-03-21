namespace ReservationSystem.Microservices.Customer.Application.SettlePoints;

/// <summary>
/// Command carrying the data needed to settle a previously authorised points redemption.
/// </summary>
public sealed record SettlePointsCommand(
    string LoyaltyNumber,
    string RedemptionReference);
