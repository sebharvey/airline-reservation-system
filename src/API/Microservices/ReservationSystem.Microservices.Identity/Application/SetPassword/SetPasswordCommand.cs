namespace ReservationSystem.Microservices.Identity.Application.SetPassword;

/// <summary>
/// Command to set a password directly on a user account (admin-initiated, no token required).
/// The caller is responsible for hashing the password before issuing this command.
/// </summary>
public sealed record SetPasswordCommand(
    Guid UserAccountId,
    string NewPasswordHash);
