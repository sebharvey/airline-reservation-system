using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Infrastructure.ExternalServices;

namespace ReservationSystem.Microservices.Delivery.Application.OciCheckIn;

public sealed record OciCheckInTicket(
    string TicketNumber,
    string PassengerId,
    string GivenName,
    string Surname,
    string? DocNationality = null,
    string? DocNumber = null,
    string? DocIssuingCountry = null,
    string? DocExpiryDate = null);

public sealed record OciCheckInCommand(string DepartureAirport, IReadOnlyList<OciCheckInTicket> Tickets);

public sealed record OciCheckInTicketResult(string TicketNumber, string Status);

public sealed record TimaticNote(
    string CheckType,    // "DOC" or "APIS"
    string TicketNumber,
    string Status,       // "PASS" or "FAIL"
    string Detail,
    string Timestamp);

public sealed class TimaticValidationException : Exception
{
    public IReadOnlyList<TimaticNote> TimaticNotes { get; }

    public TimaticValidationException(string message, IReadOnlyList<TimaticNote> notes)
        : base(message) => TimaticNotes = notes;
}

public sealed record OciCheckInResult(
    int CheckedIn,
    IReadOnlyList<OciCheckInTicketResult> Tickets,
    IReadOnlyList<TimaticNote> TimaticNotes);

public sealed class OciCheckInHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly TimaticServiceClient _timaticServiceClient;
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(
        ITicketRepository ticketRepository,
        TimaticServiceClient timaticServiceClient,
        ILogger<OciCheckInHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _timaticServiceClient = timaticServiceClient;
        _logger = logger;
    }

    public async Task<OciCheckInResult> HandleAsync(OciCheckInCommand command, CancellationToken cancellationToken = default)
    {
        var checkedInCount = 0;
        var results = new List<OciCheckInTicketResult>();
        var timaticNotes = new List<TimaticNote>();

        // Tickets that were checked in but have no seat yet — collected for group allocation.
        var pendingAssignment = new List<(Domain.Entities.Ticket Ticket, string FlightNumber, string CabinCode)>();

        // ── Phase 1: Timatic validation ─────────────────────────────────────────
        // Run document check and APIS check for every ticket before touching any
        // coupon status. A rejection from either check blocks the entire check-in.
        foreach (var ticketRequest in command.Tickets)
        {
            var ticket = await _ticketRepository.GetByETicketNumberAsync(ticketRequest.TicketNumber, cancellationToken);
            if (ticket is null)
            {
                _logger.LogWarning("Ticket {TicketNumber} not found — skipping Timatic validation", ticketRequest.TicketNumber);
                continue;
            }

            // Resolve coupon for the departure airport to get flight segment details
            var coupon = ticket.GetCouponForOrigin(command.DepartureAirport);
            if (coupon is null)
            {
                _logger.LogWarning("No coupon for {DepartureAirport} on ticket {TicketNumber} — skipping Timatic validation", command.DepartureAirport, ticketRequest.TicketNumber);
                continue;
            }

            var bookingRef = ticket.BookingReference;
            var ticketNumSafe = ticketRequest.TicketNumber.Replace("-", "");

            // Document check — validates passport/visa requirements
            if (!string.IsNullOrWhiteSpace(ticketRequest.DocNumber))
            {
                var docRequest = new TimaticDocumentCheckRequest
                {
                    TransactionIdentifier = $"TXN-{bookingRef}-DOC-{ticketNumSafe}",
                    AirlineCode = "AX",
                    JourneyType = "OW",
                    PaxInfo = new TimaticDocCheckPaxInfo
                    {
                        DocumentType = "P",
                        Nationality = ticketRequest.DocNationality ?? string.Empty,
                        DocumentIssuerCountry = ticketRequest.DocIssuingCountry ?? string.Empty,
                        DocumentNumber = ticketRequest.DocNumber,
                        DocumentExpiryDate = ticketRequest.DocExpiryDate ?? string.Empty,
                        Gender = "X",
                        ResidentCountry = ticketRequest.DocNationality ?? string.Empty
                    },
                    Itinerary =
                    [
                        new TimaticItinerarySegment
                        {
                            DepartureAirport = coupon.Origin,
                            ArrivalAirport = coupon.Destination,
                            Airline = "AX",
                            FlightNumber = coupon.FlightNumber,
                            DepartureDate = NormaliseDate(coupon.DepartureDate)
                        }
                    ]
                };

                var docResult = await _timaticServiceClient.DocumentCheckAsync(docRequest, cancellationToken);

                var docTimestamp = DateTime.UtcNow.ToString("o");
                if (!string.Equals(docResult.Status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    var requirements = docResult.Requirements
                        .Where(r => r.Mandatory)
                        .Select(r => r.Description)
                        .ToList();

                    var detail = requirements.Count > 0
                        ? string.Join("; ", requirements)
                        : "Travel document check failed.";

                    _logger.LogWarning(
                        "Timatic document check failed for ticket {TicketNumber}: {Detail}",
                        ticketRequest.TicketNumber, detail);

                    timaticNotes.Add(new TimaticNote("DOC", ticketRequest.TicketNumber, "FAIL", detail, docTimestamp));
                    throw new TimaticValidationException(
                        $"Travel document check failed for passenger {ticketRequest.GivenName} {ticketRequest.Surname}: {detail}",
                        timaticNotes);
                }

                timaticNotes.Add(new TimaticNote("DOC", ticketRequest.TicketNumber, "PASS", "Document check passed.", docTimestamp));
                _logger.LogInformation(
                    "Timatic document check passed for ticket {TicketNumber}", ticketRequest.TicketNumber);
            }

            // APIS check — validates Advance Passenger Information
            var apisRequest = new TimaticApisCheckRequest
            {
                TransactionIdentifier = $"TXN-{bookingRef}-APIS-{ticketNumSafe}",
                AirlineCode = "AX",
                FlightNumber = coupon.FlightNumber,
                DepartureDate = NormaliseDate(coupon.DepartureDate),
                DepartureAirport = coupon.Origin,
                ArrivalAirport = coupon.Destination,
                PaxInfo = new TimaticApisPaxInfo
                {
                    Surname = ticketRequest.Surname.ToUpperInvariant(),
                    GivenNames = ticketRequest.GivenName.ToUpperInvariant(),
                    Gender = "X",
                    Nationality = ticketRequest.DocNationality ?? string.Empty,
                    DocumentType = "P",
                    DocumentNumber = ticketRequest.DocNumber ?? string.Empty,
                    DocumentIssuerCountry = ticketRequest.DocIssuingCountry ?? string.Empty,
                    DocumentExpiryDate = ticketRequest.DocExpiryDate ?? string.Empty
                }
            };

            var apisResult = await _timaticServiceClient.ApisCheckAsync(apisRequest, cancellationToken);

            var apisTimestamp = DateTime.UtcNow.ToString("o");
            if (!string.Equals(apisResult.ApisStatus, "ACCEPTED", StringComparison.OrdinalIgnoreCase))
            {
                var warnings = apisResult.Warnings.Select(w => w.Description).ToList();
                var detail = warnings.Count > 0 ? string.Join("; ", warnings) : "APIS check rejected.";

                _logger.LogWarning(
                    "Timatic APIS check rejected for ticket {TicketNumber}: {Detail}",
                    ticketRequest.TicketNumber, detail);

                timaticNotes.Add(new TimaticNote("APIS", ticketRequest.TicketNumber, "FAIL", detail, apisTimestamp));
                throw new TimaticValidationException(
                    $"APIS check failed for passenger {ticketRequest.GivenName} {ticketRequest.Surname}: {detail}",
                    timaticNotes);
            }

            var apisDetail = string.IsNullOrWhiteSpace(apisResult.AuditRef)
                ? "APIS check accepted."
                : $"APIS check accepted. Audit ref: {apisResult.AuditRef}";
            timaticNotes.Add(new TimaticNote("APIS", ticketRequest.TicketNumber, "PASS", apisDetail, apisTimestamp));
            _logger.LogInformation(
                "Timatic APIS check accepted for ticket {TicketNumber} — audit ref {AuditRef}",
                ticketRequest.TicketNumber, apisResult.AuditRef);
        }

        // ── Phase 2: check in coupons ────────────────────────────────────────────
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

        // ── Phase 3: auto-assign seats, grouping by flight ───────────────────────
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

        return new OciCheckInResult(checkedInCount, results, timaticNotes);
    }

    private static string NormaliseDate(string raw)
    {
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }
}
