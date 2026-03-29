namespace ReservationSystem.Microservices.Identity.Application.UpdateAccount;

/// <summary>
/// Command to apply an admin-initiated update to a user account.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateAccountCommand(
    Guid UserAccountId,
    string? Email,
    bool? IsLocked);
