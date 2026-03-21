namespace ReservationSystem.Microservices.Schedule.Application.CreateSchedule;

public sealed record CreateScheduleCommand(
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo);
