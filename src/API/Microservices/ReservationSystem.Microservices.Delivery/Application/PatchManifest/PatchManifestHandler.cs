using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.PatchManifest;

public sealed class PatchManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<PatchManifestHandler> _logger;

    public PatchManifestHandler(
        IManifestRepository manifestRepository,
        ITicketRepository ticketRepository,
        ILogger<PatchManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(string bookingReference, PatchManifestRequest request, CancellationToken cancellationToken = default)
    {
        var updatedCount = 0;

        foreach (var update in request.Updates)
        {
            var manifest = await _manifestRepository.GetByInventoryAndPassengerAsync(
                update.InventoryId, update.PassengerId, cancellationToken);

            if (manifest is null)
            {
                _logger.LogWarning("No manifest found for {BookingRef} / inventory {InventoryId} / passenger {PassengerId}",
                    bookingReference, update.InventoryId, update.PassengerId);
                continue;
            }

            if (update.CheckedIn == true)
            {
                var checkedInAt = !string.IsNullOrWhiteSpace(update.CheckedInAt)
                    ? DateTime.Parse(update.CheckedInAt)
                    : DateTime.UtcNow;
                manifest.UpdateCheckIn(true, checkedInAt);

                var ticket = await _ticketRepository.GetByETicketNumberAsync(manifest.ETicketNumber, cancellationToken);
                if (ticket is not null)
                {
                    var updated = ticket.UpdateCouponStatus(
                        manifest.FlightNumber, manifest.Origin, manifest.Destination,
                        newStatus: "C", actor: "DeliveryMS");

                    if (updated)
                        await _ticketRepository.UpdateAsync(ticket, cancellationToken);
                    else
                        _logger.LogWarning(
                            "No matching coupon found on ticket {ETicketNumber} for flight {FlightNumber} {Origin}-{Destination}",
                            manifest.ETicketNumber, manifest.FlightNumber, manifest.Origin, manifest.Destination);
                }
                else
                {
                    _logger.LogWarning("No ticket found for ETicketNumber {ETicketNumber}", manifest.ETicketNumber);
                }
            }

            if (update.SsrCodes is not null)
            {
                var ssrJson = JsonSerializer.Serialize(update.SsrCodes, SharedJsonOptions.CamelCase);
                manifest.UpdateSsrCodes(ssrJson);
            }

            await _manifestRepository.UpdateAsync(manifest, cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }
}
