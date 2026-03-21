using System.Text.Json;
using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;
using ReservationSystem.Shared.Common.Json;

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

        var cabinFaresJson = request.CabinFares is not null
            ? JsonSerializer.Serialize(request.CabinFares, SharedJsonOptions.CamelCase)
            : "[]";

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
            CabinFares: cabinFaresJson,
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
        JsonElement? cabinFareDefinitions = null;
        if (!string.IsNullOrWhiteSpace(schedule.CabinFares))
        {
            cabinFareDefinitions = JsonSerializer.Deserialize<JsonElement>(schedule.CabinFares);
        }

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
            CabinFareDefinitions = cabinFareDefinitions,
            CreatedBy = schedule.CreatedBy,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    public static IReadOnlyList<ScheduleResponse> ToResponse(IEnumerable<Domain.Entities.FlightSchedule> schedules) =>
        schedules.Select(ToResponse).ToList().AsReadOnly();
}
