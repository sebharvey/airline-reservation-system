namespace ReservationSystem.Orchestration.Admin.Application.DeleteUser;

public sealed record DeleteUserCommand(Guid UserId, Guid StaffUserId);
