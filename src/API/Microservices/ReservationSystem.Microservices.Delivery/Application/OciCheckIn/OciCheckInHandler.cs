using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.OciCheckIn;

public sealed record OciCheckInTicket(string TicketNumber, string PassengerId, string GivenName, string Surname);

public sealed record OciCheckInCommand(string DepartureAirport, IReadOnlyList<OciCheckInTicket> Tickets);

public sealed record OciCheckInTicketResult(string TicketNumber, string Status);

public sealed record OciCheckInResult(int CheckedIn, IReadOnlyList<OciCheckInTicketResult> Tickets);

public sealed class OciCheckInHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(
        ITicketRepository ticketRepository,
        ILogger<OciCheckInHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<OciCheckInResult> HandleAsync(OciCheckInCommand command, CancellationToken cancellationToken = default)
    {
        var checkedInCount = 0;
        var results = new List<OciCheckInTicketResult>();

        foreach (var ticketRequest in command.Tickets)
        {
            var ticket = await _ticketRepository.GetByETicketNumberAsync(ticketRequest.TicketNumber, cancellationToken);
            if (ticket is null)
            {
                _logger.LogWarning("Ticket {TicketNumber} not found for check-in", ticketRequest.TicketNumber);
                results.Add(new OciCheckInTicketResult(ticketRequest.TicketNumber, "NOTFOUND"));
                continue;
            }

            var updated = ticket.CheckInCouponsForOrigin(command.DepartureAirport, "OCI");

            if (updated > 0)
            {
                await _ticketRepository.UpdateAsync(ticket, cancellationToken);
                checkedInCount++;
            }

            results.Add(new OciCheckInTicketResult(ticketRequest.TicketNumber, updated > 0 ? "C" : "O"));

            _logger.LogInformation(
                "Checked in ticket {TicketNumber} for departure from {DepartureAirport} ({Count} coupon(s) updated)",
                ticketRequest.TicketNumber, command.DepartureAirport, updated);
        }

        return new OciCheckInResult(checkedInCount, results);
    }
}
