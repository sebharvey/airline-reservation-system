namespace ReservationSystem.Orchestration.Loyalty.Application.VerifyEmailChange;

public sealed record VerifyEmailChangeCommand(string Token, string NewEmail);
