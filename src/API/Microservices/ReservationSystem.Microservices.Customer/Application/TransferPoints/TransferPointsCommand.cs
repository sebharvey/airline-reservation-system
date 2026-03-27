namespace ReservationSystem.Microservices.Customer.Application.TransferPoints;

/// <summary>
/// Command carrying the data needed to transfer points between two loyalty accounts.
/// </summary>
public sealed record TransferPointsCommand(
    string SenderLoyaltyNumber,
    string RecipientLoyaltyNumber,
    int Points);
