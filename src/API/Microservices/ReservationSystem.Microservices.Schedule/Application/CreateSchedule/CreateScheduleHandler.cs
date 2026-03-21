using Microsoft.Extensions.Logging;
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

    public async Task<Domain.Entities.FlightSchedule> HandleAsync(
        CreateScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
