using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciCheckIn;

public sealed record OciCheckInCommand(
    string BookingReference,
    string DepartureAirport);

public sealed record OciCheckInResult(
    string BookingReference,
    IReadOnlyList<string> CheckedIn,
    bool AlreadyCheckedIn);

public sealed class OciCheckInHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CheckInNoteService _noteService;
    private readonly WatchlistService _watchlistService;
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CheckInNoteService noteService,
        WatchlistService watchlistService,
        ILogger<OciCheckInHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _noteService = noteService;
        _watchlistService = watchlistService;
        _logger = logger;
    }

    public async Task<OciCheckInResult?> HandleAsync(OciCheckInCommand command, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);

        if (order is null)
        {
            _logger.LogWarning("OCI check-in: order not found for {BookingReference}", command.BookingReference);
            return null;
        }

        var tickets = BuildCheckInTickets(order.OrderData);

        var paxNameByTicket = tickets.ToDictionary(
            t => t.TicketNumber,
            t => string.IsNullOrWhiteSpace(t.GivenName) ? t.Surname : $"{t.GivenName} {t.Surname}".Trim(),
            StringComparer.OrdinalIgnoreCase);

        var paxIdByTicket = tickets.ToDictionary(
            t => t.TicketNumber,
            t => t.PassengerId,
            StringComparer.OrdinalIgnoreCase);

        if (tickets.Count == 0)
        {
            _logger.LogWarning("OCI check-in: no tickets found for {BookingReference}", command.BookingReference);
            return new OciCheckInResult(command.BookingReference, [], false);
        }

        // Watchlist check — runs before Timatic; blocks OLCI completely on any match
        var watchlistMatches = await _watchlistService.CheckAsync(
            tickets.Select(t => (t.PassengerId, t.TicketNumber, t.GivenName, t.Surname, (string?)t.DocNumber)),
            ct);

        if (watchlistMatches.Count > 0)
        {
            await _noteService.SaveAsync(
                command.BookingReference,
                CheckInHelper.BuildWatchlistNotes(watchlistMatches),
                "OCI check-in",
                ct);

            throw new InvalidOperationException(
                "Online check-in is not available for this booking. Please visit the airport check-in desk.");
        }

        ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.OciCheckInResult result;
        try
        {
            result = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, tickets, ct);
        }
        catch (OciTimaticBlockedException ex)
        {
            await _noteService.SaveAsync(
                command.BookingReference,
                CheckInHelper.BuildTimaticNotes(ex.TimaticNotes, paxNameByTicket, paxIdByTicket),
                "OCI check-in",
                ct);
            throw new InvalidOperationException(ex.Message);
        }

        var checkedInSet = result.Tickets.ToDictionary(t => t.TicketNumber, t => t.Status, StringComparer.OrdinalIgnoreCase);

        var newlyCheckedIn = result.Tickets
            .Where(t => string.Equals(t.Status, "C", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TicketNumber)
            .ToList();

        var alreadyCheckedInTickets = result.Tickets
            .Where(t => string.Equals(t.Status, "ALREADY_CHECKED_IN", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TicketNumber)
            .ToList();

        // All passengers on this segment were already checked in — skip re-persisting to order
        var allAlreadyCheckedIn = newlyCheckedIn.Count == 0 && alreadyCheckedInTickets.Count > 0;

        if (!allAlreadyCheckedIn)
        {
            var paxCheckIn = BuildPassengerCheckInEntries(tickets, checkedInSet);
            var checkedInAt = DateTime.UtcNow.ToString("o");

            try
            {
                await _orderServiceClient.UpdateOrderCheckInAsync(
                    command.BookingReference,
                    command.DepartureAirport,
                    checkedInAt,
                    paxCheckIn,
                    CheckInHelper.BuildTimaticNotes(result.TimaticNotes, paxNameByTicket, paxIdByTicket),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OCI check-in: failed to persist check-in status on order {BookingReference}",
                    command.BookingReference);
                // Non-fatal: Delivery MS has already checked in the tickets; log and continue.
            }
        }
        else
        {
            _logger.LogInformation(
                "OCI check-in: all passengers on {BookingReference} from {DepartureAirport} are already checked in",
                command.BookingReference, command.DepartureAirport);
        }

        // Return already-checked-in ticket numbers so the caller can retrieve boarding passes
        var checkedIn = allAlreadyCheckedIn ? alreadyCheckedInTickets : newlyCheckedIn;
        return new OciCheckInResult(command.BookingReference, checkedIn, allAlreadyCheckedIn);
    }

    private static List<OrderCheckInPassenger> BuildPassengerCheckInEntries(
        List<OciCheckInTicket> tickets,
        Dictionary<string, string> checkedInSet)
    {
        return tickets.Select(t =>
        {
            checkedInSet.TryGetValue(t.TicketNumber, out var ticketStatus);
            var isCheckedIn = string.Equals(ticketStatus, "C", StringComparison.OrdinalIgnoreCase);
            var status = isCheckedIn ? "CheckedIn" : "Failed";
            var name = string.IsNullOrWhiteSpace(t.GivenName) ? t.Surname : $"{t.GivenName} {t.Surname}".Trim();
            var message = isCheckedIn
                ? $"Check-in successful for {name}"
                : $"Check-in failed for {name}";

            return new OrderCheckInPassenger
            {
                PassengerId  = t.PassengerId,
                TicketNumber = t.TicketNumber,
                Status       = status,
                Message      = message
            };
        }).ToList();
    }

    private static List<OciCheckInTicket> BuildCheckInTickets(System.Text.Json.JsonElement? orderData)
    {
        var (ticketToPaxId, paxIdToInfo) = CheckInHelper.ParseOrderLookups(orderData);
        return ticketToPaxId.Select(kvp =>
        {
            paxIdToInfo.TryGetValue(kvp.Value, out var info);
            return new OciCheckInTicket
            {
                TicketNumber      = kvp.Key,
                PassengerId       = kvp.Value,
                GivenName         = info?.GivenName ?? "",
                Surname           = info?.Surname ?? "",
                DocNationality    = info?.DocNationality,
                DocNumber         = info?.DocNumber,
                DocIssuingCountry = info?.DocIssuingCountry,
                DocExpiryDate     = info?.DocExpiryDate
            };
        }).ToList();
    }
}
