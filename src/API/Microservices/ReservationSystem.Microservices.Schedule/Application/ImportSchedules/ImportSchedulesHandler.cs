using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.ImportSchedules;

/// <summary>
/// Handles bulk schedule import into a specific schedule group.
/// Deletes all existing FlightSchedule records for that group and replaces them with the incoming set.
/// </summary>
public sealed class ImportSchedulesHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly ILogger<ImportSchedulesHandler> _logger;

    public ImportSchedulesHandler(
        IFlightScheduleRepository repository,
        IScheduleGroupRepository groupRepository,
        ILogger<ImportSchedulesHandler> logger)
    {
        _repository = repository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<FlightSchedule> Schedules, int Deleted)> HandleAsync(
        ImportSchedulesCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = await _groupRepository.GetByIdAsync(command.ScheduleGroupId, cancellationToken);
        if (group is null)
            throw new ArgumentException($"Schedule group '{command.ScheduleGroupId}' not found.");

        var newSchedules = command.Schedules
            .Select(s => FlightSchedule.Create(
                command.ScheduleGroupId,
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

        var deleted = await _repository.ReplaceByGroupAsync(command.ScheduleGroupId, newSchedules, cancellationToken);

        _logger.LogInformation(
            "Schedule import complete for group '{GroupId}': {Imported} schedules imported, {Deleted} previous records deleted",
            command.ScheduleGroupId, newSchedules.Count, deleted);

        return (newSchedules.AsReadOnly(), deleted);
    }
}
