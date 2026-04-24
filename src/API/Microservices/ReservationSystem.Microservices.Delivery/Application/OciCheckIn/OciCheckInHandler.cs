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

        // Tickets that were checked in but have no seat yet — collected for group allocation.
        var pendingAssignment = new List<(Domain.Entities.Ticket Ticket, string FlightNumber, string CabinCode)>();

        // ── Phase 1: check in coupons ────────────────────────────────────────
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
                checkedInCount++;

                // Check whether the freshly checked-in coupon already has a seat.
                var unseatedCoupon = ticket.GetCheckedInCouponsForOrigin(command.DepartureAirport)
                    .FirstOrDefault(c => string.IsNullOrWhiteSpace(c.SeatNumber));

                if (unseatedCoupon is not null)
                    // Defer save until after group seat allocation.
                    pendingAssignment.Add((ticket, unseatedCoupon.FlightNumber, unseatedCoupon.ClassOfService));
                else
                    await _ticketRepository.UpdateAsync(ticket, cancellationToken);

                results.Add(new OciCheckInTicketResult(ticketRequest.TicketNumber, "C"));
            }
            else
            {
                // Distinguish "already checked in" from "no matching coupon for this airport"
                var wasAlreadyCheckedIn = ticket.GetCheckedInCouponsForOrigin(command.DepartureAirport).Count > 0;
                results.Add(new OciCheckInTicketResult(ticketRequest.TicketNumber, wasAlreadyCheckedIn ? "ALREADY_CHECKED_IN" : "O"));
            }

            _logger.LogInformation(
                "Checked in ticket {TicketNumber} for departure from {DepartureAirport} ({Count} coupon(s) updated)",
                ticketRequest.TicketNumber, command.DepartureAirport, updated);
        }

        // ── Phase 2: auto-assign seats, grouping by flight ───────────────────
        // Grouping means passengers on the same flight are allocated together so
        // that the allocator can seat them in adjacent seats where possible.
        foreach (var flightGroup in pendingAssignment.GroupBy(t => (t.FlightNumber, t.CabinCode)))
        {
            var groupList = flightGroup.ToList();

            var takenSeats = await _ticketRepository.GetAssignedSeatsForFlightAsync(
                flightGroup.Key.FlightNumber, command.DepartureAirport, cancellationToken);

            var seats = SeatAllocator.AllocateGroupSeats(
                flightGroup.Key.CabinCode, groupList.Count, takenSeats);

            for (var i = 0; i < groupList.Count; i++)
            {
                var ticket = groupList[i].Ticket;
                var seat = i < seats.Count ? seats[i] : null;

                if (seat is not null)
                {
                    ticket.AssignSeatForOrigin(command.DepartureAirport, seat, "OCI");
                    _logger.LogInformation(
                        "Auto-assigned seat {Seat} to ticket {TicketNumber} on {FlightNumber}",
                        seat, ticket.TicketNumber, flightGroup.Key.FlightNumber);
                }
                else
                {
                    _logger.LogWarning(
                        "No seat available for auto-assignment on {FlightNumber} cabin {CabinCode}",
                        flightGroup.Key.FlightNumber, flightGroup.Key.CabinCode);
                }

                await _ticketRepository.UpdateAsync(ticket, cancellationToken);
            }
        }

        return new OciCheckInResult(checkedInCount, results);
    }
}
