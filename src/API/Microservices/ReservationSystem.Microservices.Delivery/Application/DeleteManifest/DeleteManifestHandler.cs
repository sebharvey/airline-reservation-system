using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.DeleteManifest;

public sealed class DeleteManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<DeleteManifestHandler> _logger;

    public DeleteManifestHandler(IManifestRepository manifestRepository, ILogger<DeleteManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(string bookingReference, string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default)
    {
        var deleted = await _manifestRepository.DeleteByBookingAndFlightAsync(
            bookingReference, flightNumber, departureDate, cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} manifest entries for {BookingRef}/{FlightNumber}/{Date}",
                deleted, bookingReference, flightNumber, departureDate);
        else
            _logger.LogWarning("No manifest entries found to delete for {BookingRef}/{FlightNumber}/{Date}",
                bookingReference, flightNumber, departureDate);

        return deleted;
    }
}
