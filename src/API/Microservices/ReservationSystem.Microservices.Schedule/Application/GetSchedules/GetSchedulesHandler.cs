using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.GetSchedules;

/// <summary>
/// Returns all persisted flight schedules from the Schedule domain.
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
        var schedules = await _repository.GetAllAsync(cancellationToken);

        _logger.LogInformation("GetSchedules returned {Count} schedule records", schedules.Count);

        return schedules;
    }
}
