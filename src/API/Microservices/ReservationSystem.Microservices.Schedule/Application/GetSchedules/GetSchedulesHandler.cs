using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.GetSchedules;

/// <summary>
/// Returns persisted flight schedules, optionally filtered by schedule group.
/// </summary>
public sealed class GetSchedulesHandler
{
    private readonly IFlightScheduleRepository _repository;
    private readonly ILogger<GetSchedulesHandler> _logger;

    public GetSchedulesHandler(IFlightScheduleRepository repository, ILogger<GetSchedulesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightSchedule>> HandleAsync(
        GetSchedulesQuery query,
        CancellationToken cancellationToken = default)
    {
        var schedules = query.ScheduleGroupId.HasValue
            ? await _repository.GetByGroupAsync(query.ScheduleGroupId.Value, cancellationToken)
            : await _repository.GetAllAsync(cancellationToken);

        _logger.LogInformation("GetSchedules returned {Count} schedule records", schedules.Count);

        return schedules;
    }
}
