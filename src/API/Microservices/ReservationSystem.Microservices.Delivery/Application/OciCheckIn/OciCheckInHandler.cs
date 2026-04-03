using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.OciCheckIn;

public sealed record OciCheckInTicket(string TicketNumber, string PassengerId, string GivenName, string Surname);

public sealed record OciCheckInCommand(string DepartureAirport, IReadOnlyList<OciCheckInTicket> Tickets);

public sealed record OciCheckInTicketResult(string TicketNumber, string Status);

public sealed record OciCheckInResult(int CheckedIn, IReadOnlyList<OciCheckInTicketResult> Tickets);

public sealed class OciCheckInHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(
        IManifestRepository manifestRepository,
        ITicketRepository ticketRepository,
        ILogger<OciCheckInHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<OciCheckInResult> HandleAsync(OciCheckInCommand command, CancellationToken cancellationToken = default)
    {
        var checkedInCount = 0;
        var results = new List<OciCheckInTicketResult>();

        foreach (var ticketRequest in command.Tickets)
        {
            var manifests = await _manifestRepository.GetByETicketNumberAsync(ticketRequest.TicketNumber, cancellationToken);

            // Filter to the manifests departing from the given airport (outbound segments only)
            var departingManifests = manifests
                .Where(m => string.Equals(m.Origin, command.DepartureAirport, StringComparison.OrdinalIgnoreCase)
                         && !m.CheckedIn)
                .ToList();

            foreach (var manifest in departingManifests)
            {
                manifest.UpdateCheckIn(true, DateTime.UtcNow);
                await _manifestRepository.UpdateAsync(manifest, cancellationToken);
            }

            // Update coupon status to "C" (checked-in) on the ticket
            var ticket = await _ticketRepository.GetByETicketNumberAsync(ticketRequest.TicketNumber, cancellationToken);
            if (ticket is not null)
            {
                foreach (var manifest in departingManifests)
                {
                    ticket.UpdateCouponStatus(manifest.FlightNumber, manifest.Origin, manifest.Destination, "C", "OCI");
                }
                if (departingManifests.Count > 0)
                    await _ticketRepository.UpdateAsync(ticket, cancellationToken);
            }

            if (departingManifests.Count > 0)
                checkedInCount++;

            results.Add(new OciCheckInTicketResult(ticketRequest.TicketNumber, "C"));

            _logger.LogInformation(
                "Checked in ticket {TicketNumber} for departure from {DepartureAirport} ({Count} segment(s))",
                ticketRequest.TicketNumber, command.DepartureAirport, departingManifests.Count);
        }

        return new OciCheckInResult(checkedInCount, results);
    }
}
