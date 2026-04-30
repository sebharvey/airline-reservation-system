using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.DeleteManifestFlight;

public sealed class DeleteManifestFlightHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<DeleteManifestFlightHandler> _logger;

    public DeleteManifestFlightHandler(
        IManifestRepository manifestRepository,
        ILogger<DeleteManifestFlightHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        DeleteManifestFlightCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Deleting manifest entries for booking {BookingReference} on flight {FlightNumber}/{DepartureDate}",
            command.BookingReference, command.FlightNumber, command.DepartureDate);

        return await _manifestRepository.DeleteByBookingAndFlightAsync(
            command.BookingReference, command.FlightNumber, command.DepartureDate, cancellationToken);
    }
}
