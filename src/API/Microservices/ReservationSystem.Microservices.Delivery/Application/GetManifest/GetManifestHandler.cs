using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetManifest;

public sealed class GetManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<GetManifestHandler> _logger;

    public GetManifestHandler(
        IManifestRepository manifestRepository,
        ILogger<GetManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Manifest>> HandleAsync(
        GetManifestQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving manifest for flight {FlightNumber} on {DepartureDate}",
            query.FlightNumber, query.DepartureDate);

        return await _manifestRepository.GetByFlightAsync(
            query.FlightNumber, query.DepartureDate, cancellationToken);
    }
}
