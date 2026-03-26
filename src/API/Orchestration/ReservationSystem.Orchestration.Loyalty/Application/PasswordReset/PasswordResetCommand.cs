namespace ReservationSystem.Orchestration.Loyalty.Application.PasswordReset;

public sealed record PasswordResetCommand(string Token, string NewPassword);
