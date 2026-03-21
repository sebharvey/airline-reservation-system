namespace ReservationSystem.Microservices.Identity.Application.ResetPassword;

/// <summary>
/// Command carrying the data needed to complete a password reset.
/// </summary>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword);
