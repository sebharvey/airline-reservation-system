using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;

namespace ReservationSystem.Microservices.Schedule.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations for Schedules.
///
/// Mapping directions:
///   HTTP request  →  Application command
///   Domain entity →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class ScheduleMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateScheduleCommand ToCommand(CreateScheduleRequest request) =>
        new(
            FlightNumber: request.FlightNumber,
            Origin: request.Origin,
            Destination: request.Destination,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    public static UpdateScheduleCommand ToCommand(Guid scheduleId, UpdateScheduleRequest request) =>
        new(
            ScheduleId: scheduleId,
            FlightsCreatedCount: request.FlightsCreatedCount);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static ScheduleResponse ToResponse(Domain.Entities.FlightSchedule schedule) =>
        new()
        {
            ScheduleId = schedule.ScheduleId,
            FlightNumber = schedule.FlightNumber,
            Origin = schedule.Origin,
            Destination = schedule.Destination,
            ValidFrom = schedule.ValidFrom,
            ValidTo = schedule.ValidTo,
            FlightsCreatedCount = schedule.FlightsCreatedCount,
            IsActive = schedule.IsActive,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };

    public static IReadOnlyList<ScheduleResponse> ToResponse(IEnumerable<Domain.Entities.FlightSchedule> schedules) =>
        schedules.Select(ToResponse).ToList().AsReadOnly();
}
