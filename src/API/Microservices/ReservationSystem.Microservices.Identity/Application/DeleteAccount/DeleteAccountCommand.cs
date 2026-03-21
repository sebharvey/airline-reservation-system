namespace ReservationSystem.Microservices.Identity.Application.DeleteAccount;

/// <summary>
/// Command carrying the data needed to delete a user account.
/// </summary>
public sealed record DeleteAccountCommand(
    Guid UserAccountId);
