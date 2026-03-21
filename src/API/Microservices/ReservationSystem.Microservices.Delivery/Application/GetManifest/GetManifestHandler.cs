using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetManifest;

public sealed class GetManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<GetManifestHandler> _logger;

    public GetManifestHandler(IManifestRepository manifestRepository, ILogger<GetManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<GetManifestResponse?> HandleAsync(string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default)
    {
        var manifests = await _manifestRepository.GetByFlightAsync(flightNumber, departureDate, cancellationToken);

        if (manifests.Count == 0)
        {
            _logger.LogWarning("No manifest entries found for {FlightNumber} on {Date}", flightNumber, departureDate);
            return null;
        }

        return new GetManifestResponse
        {
            FlightNumber = flightNumber,
            DepartureDate = departureDate.ToString("yyyy-MM-dd"),
            TotalPassengers = manifests.Count,
            Entries = manifests.Select(DeliveryMapper.ToManifestDetail).ToList()
        };
    }
}
