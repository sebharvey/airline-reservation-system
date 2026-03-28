namespace ReservationSystem.Microservices.User.Application.ResetPassword;

/// <summary>
/// Command to reset an employee user's password.
/// </summary>
public sealed record ResetPasswordCommand(Guid UserId, string NewPassword);
