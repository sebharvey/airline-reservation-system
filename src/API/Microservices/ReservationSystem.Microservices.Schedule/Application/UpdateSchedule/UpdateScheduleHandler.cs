using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;

public sealed class UpdateScheduleHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly ILogger<UpdateScheduleHandler> _logger;

    public UpdateScheduleHandler(IFlightScheduleRepository repository, ILogger<UpdateScheduleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightSchedule?> HandleAsync(
        UpdateScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _repository.GetByIdAsync(command.ScheduleId, cancellationToken);

        if (schedule is null)
        {
            _logger.LogWarning("Update requested for unknown schedule {ScheduleId}", command.ScheduleId);
            return null;
        }

        schedule.UpdateFlightsCreated(command.FlightsCreated);
        await _repository.UpdateAsync(schedule, cancellationToken);

        _logger.LogInformation("Updated schedule {ScheduleId} — FlightsCreated set to {Count}",
            schedule.ScheduleId, command.FlightsCreated);

        return schedule;
    }
}
