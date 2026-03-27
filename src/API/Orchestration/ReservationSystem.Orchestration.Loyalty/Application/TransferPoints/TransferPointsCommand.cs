namespace ReservationSystem.Orchestration.Loyalty.Application.TransferPoints;

/// <summary>
/// Command carrying the data needed to initiate a points transfer via the orchestration layer.
/// </summary>
public sealed record TransferPointsCommand(
    string SenderLoyaltyNumber,
    string RecipientLoyaltyNumber,
    string RecipientEmail,
    int Points);
