using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.AdminCheckIn;

public sealed record AdminCheckInTravelDocument(
    string Type,
    string Number,
    string IssuingCountry,
    string Nationality,
    string IssueDate,
    string ExpiryDate);

public sealed record AdminCheckInPassenger(
    string TicketNumber,
    AdminCheckInTravelDocument TravelDocument);

public sealed record AdminCheckInCommand(
    string BookingReference,
    string DepartureAirport,
    IReadOnlyList<AdminCheckInPassenger> Passengers,
    bool OverrideTimatic = false,
    string? OverrideReason = null);

public sealed record AdminCheckInResult(
    string BookingReference,
    IReadOnlyList<OciBoardingCard> BoardingCards,
    IReadOnlyList<OciTimaticNote> TimaticNotes);

public sealed class AdminCheckInHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CheckInNoteService _noteService;
    private readonly WatchlistService _watchlistService;
    private readonly ILogger<AdminCheckInHandler> _logger;

    public AdminCheckInHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CheckInNoteService noteService,
        WatchlistService watchlistService,
        ILogger<AdminCheckInHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _noteService = noteService;
        _watchlistService = watchlistService;
        _logger = logger;
    }

    public async Task<AdminCheckInResult?> HandleAsync(AdminCheckInCommand command, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);
        if (order is null)
        {
            _logger.LogWarning("Admin check-in: order not found for {BookingReference}", command.BookingReference);
            return null;
        }

        var (ticketToPaxId, paxIdToInfo) = CheckInHelper.ParseOrderLookups(order.OrderData);

        // Persist travel documents to the order
        var passengerUpdates = new List<PassengerDocUpdate>();
        foreach (var pax in command.Passengers)
        {
            if (!ticketToPaxId.TryGetValue(pax.TicketNumber, out var passengerId))
            {
                _logger.LogWarning(
                    "Admin check-in: ticket {TicketNumber} not found on booking {BookingReference}",
                    pax.TicketNumber, command.BookingReference);
                throw new InvalidOperationException(
                    $"Ticket '{pax.TicketNumber}' was not found on booking '{command.BookingReference}'.");
            }

            passengerUpdates.Add(new PassengerDocUpdate
            {
                PassengerId = passengerId,
                Docs =
                [
                    new PassengerDoc
                    {
                        Type           = pax.TravelDocument.Type,
                        Number         = pax.TravelDocument.Number,
                        IssuingCountry = pax.TravelDocument.IssuingCountry,
                        Nationality    = pax.TravelDocument.Nationality,
                        IssueDate      = pax.TravelDocument.IssueDate,
                        ExpiryDate     = pax.TravelDocument.ExpiryDate
                    }
                ]
            });
        }

        if (passengerUpdates.Count > 0)
            await _orderServiceClient.UpdateOrderPassengersAsync(command.BookingReference, passengerUpdates, ct);

        // Build ticket → passenger name lookup for note formatting
        var ticketToName = ticketToPaxId.ToDictionary(
            kvp => kvp.Key,
            kvp => paxIdToInfo.TryGetValue(kvp.Value, out var info)
                ? $"{info.GivenName} {info.Surname}".Trim()
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var checkInTickets = command.Passengers.Select(pax =>
        {
            ticketToPaxId.TryGetValue(pax.TicketNumber, out var paxId);
            paxIdToInfo.TryGetValue(paxId ?? string.Empty, out var info);
            return new OciCheckInTicket
            {
                TicketNumber      = pax.TicketNumber,
                PassengerId       = paxId ?? string.Empty,
                GivenName         = info?.GivenName ?? string.Empty,
                Surname           = info?.Surname ?? string.Empty,
                DocNationality    = pax.TravelDocument.Nationality,
                DocNumber         = pax.TravelDocument.Number,
                DocIssuingCountry = pax.TravelDocument.IssuingCountry,
                DocExpiryDate     = pax.TravelDocument.ExpiryDate,
            };
        }).ToList();

        // Watchlist check — runs before Timatic; surfaces to agent for override
        var watchlistMatches = await _watchlistService.CheckAsync(
            checkInTickets.Select(t => (t.PassengerId, t.TicketNumber, t.GivenName, t.Surname, (string?)t.DocNumber)),
            ct);

        if (watchlistMatches.Count > 0)
        {
            await _noteService.SaveAsync(
                command.BookingReference,
                CheckInHelper.BuildWatchlistNotes(watchlistMatches),
                "Admin check-in",
                ct);

            if (!command.OverrideTimatic)
                throw new OciWatchlistBlockedException(
                    "One or more passengers on this booking are flagged on the security watchlist. Agent override is required.",
                    watchlistMatches);

            _logger.LogWarning(
                "Admin check-in: watchlist override authorised for {BookingReference} — reason: {Reason}",
                command.BookingReference, command.OverrideReason);

            await _noteService.SaveAsync(
                command.BookingReference,
                BuildWatchlistOverrideNotes(watchlistMatches, command.OverrideReason),
                "Admin check-in",
                ct);
        }

        OciCheckInResult checkInResult;
        try
        {
            checkInResult = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, checkInTickets, ct);
        }
        catch (OciTimaticBlockedException ex)
        {
            await _noteService.SaveAsync(
                command.BookingReference,
                CheckInHelper.BuildTimaticNotes(ex.TimaticNotes, ticketToName, ticketToPaxId),
                "Admin check-in",
                ct);

            if (!command.OverrideTimatic)
                throw;

            _logger.LogWarning(
                "Admin check-in: Timatic override authorised for {BookingReference} — reason: {Reason}",
                command.BookingReference, command.OverrideReason);

            await _noteService.SaveAsync(
                command.BookingReference,
                BuildOverrideNotes(command.Passengers, ticketToName, command.OverrideReason, ticketToPaxId),
                "Admin check-in",
                ct);

            checkInResult = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, checkInTickets, ct, bypassTimatic: true);
        }

        await _noteService.SaveAsync(
            command.BookingReference,
            CheckInHelper.BuildTimaticNotes(checkInResult.TimaticNotes, ticketToName, ticketToPaxId),
            "Admin check-in",
            ct);

        var checkedInTickets = checkInResult.Tickets
            .Where(t => string.Equals(t.Status, "C", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.Status, "ALREADY_CHECKED_IN", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TicketNumber)
            .ToList();

        if (checkedInTickets.Count == 0)
        {
            _logger.LogWarning(
                "Admin check-in: no tickets checked in for {BookingReference} from {DepartureAirport}",
                command.BookingReference, command.DepartureAirport);
            return new AdminCheckInResult(command.BookingReference, [], checkInResult.TimaticNotes);
        }

        var boardingDocsResult = await _deliveryServiceClient.GetBoardingDocsAsync(
            command.DepartureAirport, checkedInTickets, ct);

        return new AdminCheckInResult(command.BookingReference, boardingDocsResult.BoardingCards, checkInResult.TimaticNotes);
    }

    private static List<OrderTimaticNote> BuildWatchlistOverrideNotes(
        IReadOnlyList<WatchlistMatch> matches,
        string? overrideReason)
    {
        var reason    = string.IsNullOrWhiteSpace(overrideReason) ? "No reason provided" : overrideReason;
        var timestamp = DateTime.UtcNow.ToString("o");
        return matches.Select(m =>
        {
            var name    = $"{m.GivenName} {m.Surname}".Trim();
            var subject = name.Length > 0 ? $"{name} (ticket {m.TicketNumber})" : $"ticket {m.TicketNumber}";
            return new OrderTimaticNote
            {
                DateTime = timestamp,
                Type     = "OCI",
                Message  = $"Watchlist override by agent for {subject}: {reason}",
                PaxId    = CheckInHelper.ExtractPaxIdInt(m.PassengerId)
            };
        }).ToList();
    }

    private static List<OrderTimaticNote> BuildOverrideNotes(
        IReadOnlyList<AdminCheckInPassenger> passengers,
        IReadOnlyDictionary<string, string>? ticketToName,
        string? overrideReason,
        IReadOnlyDictionary<string, string>? ticketToPaxId = null)
    {
        var reason    = string.IsNullOrWhiteSpace(overrideReason) ? "No reason provided" : overrideReason;
        var timestamp = DateTime.UtcNow.ToString("o");
        return passengers.Select(p =>
        {
            var paxName = ticketToName is not null
                && ticketToName.TryGetValue(p.TicketNumber, out var name)
                && !string.IsNullOrWhiteSpace(name)
                    ? name : null;
            var subject  = paxName is not null
                ? $"{paxName} (ticket {p.TicketNumber})"
                : $"ticket {p.TicketNumber}";
            var paxIdStr = ticketToPaxId is not null
                && ticketToPaxId.TryGetValue(p.TicketNumber, out var pid) ? pid : null;
            return new OrderTimaticNote
            {
                DateTime = timestamp,
                Type     = "OCI",
                Message  = $"Timatic override by agent for {subject}: {reason}",
                PaxId    = CheckInHelper.ExtractPaxIdInt(paxIdStr)
            };
        }).ToList();
    }
}
