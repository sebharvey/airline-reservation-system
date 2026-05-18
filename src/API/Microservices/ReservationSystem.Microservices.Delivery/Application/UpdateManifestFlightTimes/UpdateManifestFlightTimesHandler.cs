using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestFlightTimes;

public sealed class UpdateManifestFlightTimesHandler
{
    private readonly IManifestRepository _repository;
    private readonly ILogger<UpdateManifestFlightTimesHandler> _logger;

    public UpdateManifestFlightTimesHandler(IManifestRepository repository, ILogger<UpdateManifestFlightTimesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpdateManifestFlightTimesCommand command, CancellationToken cancellationToken = default)
    {
        var departureDate    = DateOnly.ParseExact(command.DepartureDate, "yyyy-MM-dd");
        var newDepartureTime = TimeOnly.ParseExact(command.NewDepartureTime, "HH:mm");
        var newArrivalTime   = TimeOnly.ParseExact(command.NewArrivalTime,   "HH:mm");

        var updated = await _repository.UpdateFlightTimesAsync(
            command.FlightNumber, departureDate, newDepartureTime, newArrivalTime, cancellationToken);

        _logger.LogInformation(
            "Applied delay to {Count} manifest entries for {FlightNumber}/{DepartureDate}: dep={Dep} arr={Arr}",
            updated, command.FlightNumber, command.DepartureDate, command.NewDepartureTime, command.NewArrivalTime);

        return updated;
    }
}
