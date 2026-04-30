namespace ReservationSystem.Microservices.Schedule.Application.UpdateScheduleGroup;

public sealed record UpdateScheduleGroupCommand(Guid ScheduleGroupId, string Name, DateTime SeasonStart, DateTime SeasonEnd, bool IsActive);
