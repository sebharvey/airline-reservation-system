namespace ReservationSystem.Microservices.Schedule.Application.ImportSchedules;

/// <summary>
/// Application command carrying all schedule definitions to be imported into a specific schedule group.
/// </summary>
public sealed record ImportSchedulesCommand(
    Guid ScheduleGroupId,
    IReadOnlyList<ScheduleDefinition> Schedules);

/// <summary>
/// A single validated schedule definition within an import batch.
/// </summary>
public sealed record ScheduleDefinition(
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
    string CreatedBy,
    TimeSpan? DepartureTimeUtc = null,
    TimeSpan? ArrivalTimeUtc = null,
    byte? ArrivalDayOffsetUtc = null);
