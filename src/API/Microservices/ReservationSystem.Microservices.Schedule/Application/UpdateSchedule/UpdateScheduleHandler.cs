using Microsoft.Extensions.Logging;
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

    public async Task<Domain.Entities.FlightSchedule?> HandleAsync(
        UpdateScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
