namespace ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;

/// <summary>
/// Command carrying the data needed to initiate a password reset flow.
/// </summary>
public sealed record ResetPasswordRequestCommand(
    string Email);
