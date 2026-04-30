using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSsrs;

public sealed class UpdateManifestSsrsHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<UpdateManifestSsrsHandler> _logger;

    public UpdateManifestSsrsHandler(
        IManifestRepository manifestRepository,
        ILogger<UpdateManifestSsrsHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        UpdateManifestSsrsCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating SSR codes for booking {BookingReference} across {Count} e-ticket(s)",
            command.BookingReference, command.SsrsByETicket.Count);

        return await _manifestRepository.UpdateSsrCodesByBookingAsync(
            command.BookingReference, command.SsrsByETicket, cancellationToken);
    }
}
