using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;

public sealed class UpdateManifestSeatHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<UpdateManifestSeatHandler> _logger;

    public UpdateManifestSeatHandler(
        IManifestRepository manifestRepository,
        ILogger<UpdateManifestSeatHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        UpdateManifestSeatCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating seat for e-ticket {ETicketNumber} on inventory {InventoryId} to '{NewSeatNumber}'",
            command.ETicketNumber, command.InventoryId, command.NewSeatNumber);

        return await _manifestRepository.UpdateSeatByETicketAsync(
            command.ETicketNumber, command.InventoryId, command.NewSeatNumber, cancellationToken);
    }
}
