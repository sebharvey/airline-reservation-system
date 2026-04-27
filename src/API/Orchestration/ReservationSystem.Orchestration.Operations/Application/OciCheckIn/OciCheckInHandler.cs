using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<OciCheckInHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
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

        ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.OciCheckInResult result;
        try
        {
            result = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, tickets, ct);
        }
        catch (OciTimaticBlockedException ex)
        {
            // Timatic rejected check-in — write the check audit notes to the order then surface the error
            if (ex.TimaticNotes.Count > 0)
            {
                try
                {
                    await _orderServiceClient.AddOrderNotesAsync(
                        command.BookingReference,
                        BuildOrderNotes(ex.TimaticNotes, paxNameByTicket, paxIdByTicket),
                        ct);
                }
                catch (Exception notesEx)
                {
                    _logger.LogError(notesEx,
                        "OCI check-in: failed to write timatic notes to order {BookingReference}",
                        command.BookingReference);
                }
            }
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
                    BuildOrderNotes(result.TimaticNotes, paxNameByTicket, paxIdByTicket),
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
                PassengerId = t.PassengerId,
                TicketNumber = t.TicketNumber,
                Status = status,
                Message = message
            };
        }).ToList();
    }

    private static List<OciCheckInTicket> BuildCheckInTickets(JsonElement? orderData)
    {
        var tickets = new List<OciCheckInTicket>();
        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object) return tickets;

        // Build passengerId → name + travel doc map from dataLists.passengers
        var paxMap = new Dictionary<string, PaxInfo>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty("dataLists", out var dl) &&
            dl.TryGetProperty("passengers", out var paxArr) &&
            paxArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var pax in paxArr.EnumerateArray())
            {
                var pid = pax.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
                if (pid is null) continue;

                var gn = pax.TryGetProperty("givenName", out var gnEl) ? gnEl.GetString() ?? "" : "";
                var sn = pax.TryGetProperty("surname", out var snEl) ? snEl.GetString() ?? "" : "";

                string? docNationality = null, docNumber = null, docIssuingCountry = null, docExpiryDate = null;

                if (pax.TryGetProperty("docs", out var docs) &&
                    docs.ValueKind == JsonValueKind.Array &&
                    docs.GetArrayLength() > 0)
                {
                    var doc = docs[0];
                    docNationality = doc.TryGetProperty("nationality", out var nat) ? nat.GetString() : null;
                    docNumber = doc.TryGetProperty("number", out var num) ? num.GetString() : null;
                    docIssuingCountry = doc.TryGetProperty("issuingCountry", out var ic) ? ic.GetString() : null;
                    docExpiryDate = doc.TryGetProperty("expiryDate", out var ed) ? ed.GetString() : null;
                }

                paxMap[pid] = new PaxInfo(gn, sn, docNationality, docNumber, docIssuingCountry, docExpiryDate);
            }
        }

        // Map eTickets to check-in ticket entries
        if (!el.TryGetProperty("eTickets", out var eTickets) || eTickets.ValueKind != JsonValueKind.Array)
            return tickets;

        foreach (var et in eTickets.EnumerateArray())
        {
            var ticketNum = et.TryGetProperty("eTicketNumber", out var tnEl) ? tnEl.GetString() : null;
            var paxId = et.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
            if (ticketNum is null || paxId is null) continue;

            paxMap.TryGetValue(paxId, out var info);
            tickets.Add(new OciCheckInTicket
            {
                TicketNumber = ticketNum,
                PassengerId = paxId,
                GivenName = info?.GivenName ?? "",
                Surname = info?.Surname ?? "",
                DocNationality = info?.DocNationality,
                DocNumber = info?.DocNumber,
                DocIssuingCountry = info?.DocIssuingCountry,
                DocExpiryDate = info?.DocExpiryDate
            });
        }

        return tickets;
    }

    private sealed record PaxInfo(
        string GivenName,
        string Surname,
        string? DocNationality,
        string? DocNumber,
        string? DocIssuingCountry,
        string? DocExpiryDate);

    private static List<OrderTimaticNote> BuildOrderNotes(
        IReadOnlyList<OciTimaticNote> timaticNotes,
        IReadOnlyDictionary<string, string>? paxNameByTicket = null,
        IReadOnlyDictionary<string, string>? paxIdByTicket = null)
        => timaticNotes.Select(n =>
        {
            var checkLabel = n.CheckType switch
            {
                "DOC"  => "Document check",
                "APIS" => "APIS check",
                _      => $"{n.CheckType} check"
            };
            var isFail = !string.Equals(n.Status, "PASS", StringComparison.OrdinalIgnoreCase);
            var statusText = isFail ? "failed" : "passed";
            var paxName = isFail && paxNameByTicket is not null && paxNameByTicket.TryGetValue(n.TicketNumber, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : null;
            var subject = paxName is not null
                ? $"{paxName} (ticket {n.TicketNumber})"
                : $"ticket {n.TicketNumber}";
            var message = string.IsNullOrWhiteSpace(n.Detail)
                ? $"{checkLabel} {statusText} for {subject}"
                : $"{checkLabel} {statusText} for {subject}: {n.Detail}";
            var paxIdStr = paxIdByTicket is not null && paxIdByTicket.TryGetValue(n.TicketNumber, out var pid) ? pid : null;
            return new OrderTimaticNote
            {
                DateTime = n.Timestamp,
                Type     = "TIMATIC",
                Message  = message,
                PaxId    = ExtractPaxIdInt(paxIdStr)
            };
        }).ToList();

    private static int? ExtractPaxIdInt(string? paxId)
    {
        if (string.IsNullOrEmpty(paxId)) return null;
        var dash = paxId.LastIndexOf('-');
        return dash >= 0 && int.TryParse(paxId[(dash + 1)..], out var n) ? n : null;
    }
}
