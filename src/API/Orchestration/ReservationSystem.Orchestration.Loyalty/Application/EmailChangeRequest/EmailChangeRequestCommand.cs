namespace ReservationSystem.Orchestration.Loyalty.Application.EmailChangeRequest;

public sealed record EmailChangeRequestCommand(Guid UserAccountId, string NewEmail);
