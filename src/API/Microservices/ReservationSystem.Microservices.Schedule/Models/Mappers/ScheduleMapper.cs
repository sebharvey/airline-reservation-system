using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
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

    public static CreateScheduleCommand ToCommand(CreateScheduleRequest request)
    {
        var departureTime = TimeSpan.Parse(request.DepartureTime);
        var arrivalTime = TimeSpan.Parse(request.ArrivalTime);
        var validFrom = DateTime.Parse(request.ValidFrom);
        var validTo = DateTime.Parse(request.ValidTo);

        return new CreateScheduleCommand(
            FlightNumber: request.FlightNumber,
            Origin: request.Origin,
            Destination: request.Destination,
            DepartureTime: departureTime,
            ArrivalTime: arrivalTime,
            ArrivalDayOffset: request.ArrivalDayOffset,
            DaysOfWeek: request.DaysOfWeek,
            AircraftType: request.AircraftType,
            ValidFrom: validFrom,
            ValidTo: validTo,
            CreatedBy: request.CreatedBy);
    }

    public static UpdateScheduleCommand ToCommand(Guid scheduleId, UpdateScheduleRequest request) =>
        new(
            ScheduleId: scheduleId,
            FlightsCreated: request.FlightsCreated);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static ScheduleResponse ToResponse(Domain.Entities.FlightSchedule schedule)
    {
        var operatingDates = schedule.GetOperatingDates()
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList()
            .AsReadOnly();

        return new ScheduleResponse
        {
            ScheduleId = schedule.ScheduleId,
            FlightNumber = schedule.FlightNumber,
            Origin = schedule.Origin,
            Destination = schedule.Destination,
            DepartureTime = schedule.DepartureTime.ToString(@"hh\:mm"),
            ArrivalTime = schedule.ArrivalTime.ToString(@"hh\:mm"),
            ArrivalDayOffset = schedule.ArrivalDayOffset,
            DaysOfWeek = schedule.DaysOfWeek,
            AircraftType = schedule.AircraftType,
            ValidFrom = schedule.ValidFrom.ToString("yyyy-MM-dd"),
            ValidTo = schedule.ValidTo.ToString("yyyy-MM-dd"),
            FlightsCreated = schedule.FlightsCreated,
            OperatingDates = operatingDates,
            CreatedBy = schedule.CreatedBy,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    public static IReadOnlyList<ScheduleResponse> ToResponse(IEnumerable<Domain.Entities.FlightSchedule> schedules) =>
        schedules.Select(ToResponse).ToList().AsReadOnly();

    public static CreateScheduleResponse ToCreateResponse(Domain.Entities.FlightSchedule schedule)
    {
        var operatingDates = schedule.GetOperatingDates()
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList()
            .AsReadOnly();

        return new CreateScheduleResponse
        {
            ScheduleId = schedule.ScheduleId,
            OperatingDates = operatingDates
        };
    }

    public static UpdateScheduleResponse ToUpdateResponse(Domain.Entities.FlightSchedule schedule) =>
        new()
        {
            ScheduleId = schedule.ScheduleId,
            FlightsCreated = schedule.FlightsCreated
        };

    public static ImportSsimResponse ToImportResponse(IReadOnlyList<Domain.Entities.FlightSchedule> schedules) =>
        new()
        {
            Count = schedules.Count,
            Schedules = schedules.Select(s => new ImportedScheduleItem
            {
                ScheduleId       = s.ScheduleId,
                FlightNumber     = s.FlightNumber,
                Origin           = s.Origin,
                Destination      = s.Destination,
                ValidFrom        = s.ValidFrom.ToString("yyyy-MM-dd"),
                ValidTo          = s.ValidTo.ToString("yyyy-MM-dd"),
                OperatingDateCount = s.GetOperatingDates().Count
            }).ToList().AsReadOnly()
        };
}
