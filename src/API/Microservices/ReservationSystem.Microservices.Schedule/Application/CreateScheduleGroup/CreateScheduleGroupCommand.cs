namespace ReservationSystem.Microservices.Schedule.Application.CreateScheduleGroup;

public sealed record CreateScheduleGroupCommand(string Name, DateTime SeasonStart, DateTime SeasonEnd, bool IsActive, string CreatedBy);
