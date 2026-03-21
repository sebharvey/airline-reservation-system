using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;

public sealed class UpdateManifestSeatHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<UpdateManifestSeatHandler> _logger;

    public UpdateManifestSeatHandler(IManifestRepository manifestRepository, ILogger<UpdateManifestSeatHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpdateManifestSeatRequest request, CancellationToken cancellationToken = default)
    {
        var updatedCount = 0;

        foreach (var update in request.Updates)
        {
            var manifest = await _manifestRepository.GetByInventoryAndPassengerAsync(
                update.InventoryId, update.PassengerId, cancellationToken);

            if (manifest is null)
            {
                _logger.LogWarning("No manifest found for inventory {InventoryId} / passenger {PassengerId}",
                    update.InventoryId, update.PassengerId);
                continue;
            }

            manifest.UpdateSeat(update.SeatNumber, update.ETicketNumber);
            await _manifestRepository.UpdateAsync(manifest, cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }
}
