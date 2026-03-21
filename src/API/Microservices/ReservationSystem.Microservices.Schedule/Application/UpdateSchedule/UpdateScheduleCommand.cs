namespace ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;

public sealed record UpdateScheduleCommand(
    Guid ScheduleId,
    int FlightsCreatedCount);
