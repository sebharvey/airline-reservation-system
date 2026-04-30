namespace ReservationSystem.Orchestration.Admin.Application.SetUserStatus;

public sealed record SetUserStatusCommand(
    Guid UserId,
    bool IsActive,
    Guid StaffUserId);
