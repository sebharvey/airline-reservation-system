using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateFlightTimes;

public sealed class UpdateFlightTimesHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<UpdateFlightTimesHandler> _logger;

    public UpdateFlightTimesHandler(IManifestRepository manifestRepository, ILogger<UpdateFlightTimesHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(string bookingReference, UpdateFlightTimesRequest request, CancellationToken cancellationToken = default)
    {
        var departureDate = DateTime.Parse(request.DepartureDate);
        var manifests = await _manifestRepository.GetByBookingAndFlightAsync(
            bookingReference, request.FlightNumber, departureDate, cancellationToken);

        if (manifests.Count == 0)
        {
            _logger.LogWarning("No manifest entries found for {BookingRef}/{FlightNumber}/{Date}",
                bookingReference, request.FlightNumber, request.DepartureDate);
            return 0;
        }

        var newDepartureTime = TimeSpan.Parse(request.NewDepartureTime);
        var newArrivalTime = TimeSpan.Parse(request.NewArrivalTime);

        foreach (var manifest in manifests)
        {
            // Need to re-fetch with tracking since GetByBookingAndFlightAsync uses AsNoTracking
            var tracked = await _manifestRepository.GetByInventoryAndPassengerAsync(
                manifest.InventoryId, manifest.PassengerId, cancellationToken);
            if (tracked is null) continue;

            tracked.UpdateFlightTimes(newDepartureTime, newArrivalTime);
            await _manifestRepository.UpdateAsync(tracked, cancellationToken);
        }

        _logger.LogInformation("Updated flight times for {Count} manifest entries on {FlightNumber} {Date}",
            manifests.Count, request.FlightNumber, request.DepartureDate);

        return manifests.Count;
    }
}
