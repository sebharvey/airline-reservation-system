namespace ReservationSystem.Orchestration.Operations.Application.CreateSchedule;

public sealed record CreateScheduleCommand(
    string FlightNumber,
    string Origin,
    string Destination,
    TimeOnly DepartureTime,
    TimeOnly ArrivalTime,
    string AircraftType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlyList<DayOfWeek> OperatingDays);
