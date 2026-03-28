using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.ImportSchedules;

/// <summary>
/// Handles bulk schedule import.
/// Deletes all existing FlightSchedule records and replaces them with the incoming set,
/// ensuring the database always reflects the current season schedule exactly.
/// </summary>
public sealed class ImportSchedulesHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly ILogger<ImportSchedulesHandler> _logger;

    public ImportSchedulesHandler(
        IFlightScheduleRepository repository,
        ILogger<ImportSchedulesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<FlightSchedule> Schedules, int Deleted)> HandleAsync(
        ImportSchedulesCommand command,
        CancellationToken cancellationToken = default)
    {
        var newSchedules = command.Schedules
            .Select(s => FlightSchedule.Create(
                s.FlightNumber,
                s.Origin,
                s.Destination,
                s.DepartureTime,
                s.ArrivalTime,
                s.ArrivalDayOffset,
                s.DaysOfWeek,
                s.AircraftType,
                s.ValidFrom,
                s.ValidTo,
                s.CreatedBy))
            .ToList();

        var deleted = await _repository.ReplaceAllAsync(newSchedules, cancellationToken);

        _logger.LogInformation(
            "Schedule import complete: {Imported} schedules imported, {Deleted} previous records deleted",
            newSchedules.Count, deleted);

        return (newSchedules.AsReadOnly(), deleted);
    }
}
