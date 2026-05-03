using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;

public sealed class UpdateManifestSeatHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<UpdateManifestSeatHandler> _logger;

    public UpdateManifestSeatHandler(
        IManifestRepository manifestRepository,
        ITicketRepository ticketRepository,
        ILogger<UpdateManifestSeatHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        UpdateManifestSeatCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating seat for e-ticket {ETicketNumber} on inventory {InventoryId} to '{NewSeatNumber}'",
            command.ETicketNumber, command.InventoryId, command.NewSeatNumber);

        var manifestEntry = await _manifestRepository.UpdateSeatByETicketAsync(
            command.ETicketNumber, command.InventoryId, command.NewSeatNumber, cancellationToken);

        if (manifestEntry is null)
            return false;

        // Also update the coupon seat on the e-ticket so the ticket reflects the new seat.
        var ticket = await _ticketRepository.GetByETicketNumberAsync(command.ETicketNumber, cancellationToken);
        if (ticket is not null)
        {
            ticket.OverrideSeatForOrigin(manifestEntry.Origin, command.NewSeatNumber, "StaffManifest");
            await _ticketRepository.UpdateAsync(ticket, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Ticket not found for e-ticket {ETicketNumber} — manifest seat updated but ticket coupon not updated",
                command.ETicketNumber);
        }

        return true;
    }
}
