using ReservationSystem.Microservices.Schedule.Application.ImportSchedules;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;

namespace ReservationSystem.Microservices.Schedule.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations for Schedules.
/// </summary>
public static class ScheduleMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static ImportSchedulesCommand ToCommand(ImportSchedulesRequest request)
    {
        var definitions = request.Carriers
            .SelectMany(c => c.Schedules)
            .Select(s => new ScheduleDefinition(
                FlightNumber: s.FlightNumber,
                Origin: s.Origin,
                Destination: s.Destination,
                DepartureTime: TimeSpan.Parse(s.DepartureTime),
                ArrivalTime: TimeSpan.Parse(s.ArrivalTime),
                ArrivalDayOffset: s.ArrivalDayOffset,
                DaysOfWeek: s.DaysOfWeek,
                AircraftType: s.AircraftType,
                ValidFrom: DateTime.Parse(s.ValidFrom),
                ValidTo: DateTime.Parse(s.ValidTo),
                CreatedBy: s.CreatedBy))
            .ToList()
            .AsReadOnly();

        return new ImportSchedulesCommand(request.ScheduleGroupId, definitions);
    }

    // -------------------------------------------------------------------------
    // Domain entities → HTTP response
    // -------------------------------------------------------------------------

    public static ImportSchedulesResponse ToImportResponse(
        IReadOnlyList<Domain.Entities.FlightSchedule> schedules,
        int deleted) =>
        new()
        {
            Imported = schedules.Count,
            Deleted = deleted,
            Schedules = schedules.Select(s => new ImportedScheduleSummary
            {
                ScheduleId = s.ScheduleId,
                FlightNumber = s.FlightNumber,
                Origin = s.Origin,
                Destination = s.Destination,
                ValidFrom = s.ValidFrom.ToString("yyyy-MM-dd"),
                ValidTo = s.ValidTo.ToString("yyyy-MM-dd"),
                OperatingDateCount = s.GetOperatingDates().Count
            }).ToList().AsReadOnly()
        };
}
