namespace ReservationSystem.Microservices.Schedule.Application.CreateSchedule;

public sealed record CreateScheduleCommand(
    string FlightNumber,
    string Origin,
    string Destination,
    TimeSpan DepartureTime,
    TimeSpan ArrivalTime,
    byte ArrivalDayOffset,
    byte DaysOfWeek,
    string AircraftType,
    DateTime ValidFrom,
    DateTime ValidTo,
    string CreatedBy);
