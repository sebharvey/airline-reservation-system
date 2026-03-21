using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.CreateSchedule;

public sealed class CreateScheduleHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly ILogger<CreateScheduleHandler> _logger;

    public CreateScheduleHandler(IFlightScheduleRepository repository, ILogger<CreateScheduleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightSchedule> HandleAsync(
        CreateScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var schedule = FlightSchedule.Create(
            command.FlightNumber,
            command.Origin,
            command.Destination,
            command.DepartureTime,
            command.ArrivalTime,
            command.ArrivalDayOffset,
            command.DaysOfWeek,
            command.AircraftType,
            command.ValidFrom,
            command.ValidTo,
            command.CabinFares,
            command.CreatedBy);

        await _repository.CreateAsync(schedule, cancellationToken);

        _logger.LogInformation("Created flight schedule {ScheduleId} for {FlightNumber} ({Origin}-{Destination})",
            schedule.ScheduleId, schedule.FlightNumber, schedule.Origin, schedule.Destination);

        return schedule;
    }
}
