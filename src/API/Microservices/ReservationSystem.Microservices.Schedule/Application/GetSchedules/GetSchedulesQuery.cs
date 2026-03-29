namespace ReservationSystem.Microservices.Schedule.Application.GetSchedules;

public sealed record GetSchedulesQuery(Guid? ScheduleGroupId = null);
