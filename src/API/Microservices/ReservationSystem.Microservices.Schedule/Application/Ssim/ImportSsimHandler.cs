using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.Ssim;

public sealed class ImportSsimHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly ILogger<ImportSsimHandler> _logger;

    public ImportSsimHandler(IFlightScheduleRepository repository, ILogger<ImportSsimHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightSchedule>> HandleAsync(
        ImportSsimCommand command,
        CancellationToken cancellationToken = default)
    {
        var commands = SsimParser.Parse(command.SsimText, command.CreatedBy);
        var schedules = new List<FlightSchedule>(commands.Count);

        foreach (var cmd in commands)
        {
            var schedule = FlightSchedule.Create(
                cmd.FlightNumber,
                cmd.Origin,
                cmd.Destination,
                cmd.DepartureTime,
                cmd.ArrivalTime,
                cmd.ArrivalDayOffset,
                cmd.DaysOfWeek,
                cmd.AircraftType,
                cmd.ValidFrom,
                cmd.ValidTo,
                cmd.CreatedBy);

            await _repository.CreateAsync(schedule, cancellationToken);

            _logger.LogInformation(
                "Imported schedule {ScheduleId} for {FlightNumber} ({Origin}-{Destination}) from SSIM",
                schedule.ScheduleId, schedule.FlightNumber, schedule.Origin, schedule.Destination);

            schedules.Add(schedule);
        }

        return schedules.AsReadOnly();
    }
}
