namespace ReservationSystem.Orchestration.Admin.Application.ResetPassword;

public sealed record ResetPasswordCommand(Guid UserId, string NewPassword);
