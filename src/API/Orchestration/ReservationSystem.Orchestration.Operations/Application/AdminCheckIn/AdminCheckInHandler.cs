using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AdminCheckInHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AdminCheckInHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<AdminCheckInHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
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

        var (ticketToPaxId, paxIdToName) = ParseOrderData(order.OrderData);

        // Persist travel documents to the order
        var passengerUpdates = new List<object>();
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

            passengerUpdates.Add(new
            {
                passengerId,
                docs = new[]
                {
                    new
                    {
                        type = pax.TravelDocument.Type,
                        number = pax.TravelDocument.Number,
                        issuingCountry = pax.TravelDocument.IssuingCountry,
                        nationality = pax.TravelDocument.Nationality,
                        issueDate = pax.TravelDocument.IssueDate,
                        expiryDate = pax.TravelDocument.ExpiryDate
                    }
                }
            });
        }

        if (passengerUpdates.Count > 0)
            await _orderServiceClient.UpdateOrderPassengersAsync(command.BookingReference, new { passengers = passengerUpdates }, ct);

        // Build ticket → passenger name lookup for note formatting
        var ticketToName = ticketToPaxId.ToDictionary(
            kvp => kvp.Key,
            kvp => paxIdToName.TryGetValue(kvp.Value, out var n)
                ? $"{n.GivenName} {n.Surname}".Trim()
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var checkInTickets = command.Passengers.Select(pax =>
        {
            ticketToPaxId.TryGetValue(pax.TicketNumber, out var paxId);
            paxIdToName.TryGetValue(paxId ?? string.Empty, out var name);
            return new OciCheckInTicket
            {
                TicketNumber = pax.TicketNumber,
                PassengerId = paxId ?? string.Empty,
                GivenName = name?.GivenName ?? string.Empty,
                Surname = name?.Surname ?? string.Empty,
                DocNationality = pax.TravelDocument.Nationality,
                DocNumber = pax.TravelDocument.Number,
                DocIssuingCountry = pax.TravelDocument.IssuingCountry,
                DocExpiryDate = pax.TravelDocument.ExpiryDate,
            };
        }).ToList();

        OciCheckInResult checkInResult;
        try
        {
            checkInResult = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, checkInTickets, ct);
        }
        catch (OciTimaticBlockedException ex)
        {
            if (ex.TimaticNotes.Count > 0)
            {
                try
                {
                    await _orderServiceClient.AddOrderNotesAsync(
                        command.BookingReference,
                        BuildOrderNotes(ex.TimaticNotes, ticketToName, ticketToPaxId),
                        ct);
                }
                catch (Exception notesEx)
                {
                    _logger.LogError(notesEx,
                        "Admin check-in: failed to write Timatic notes to order {BookingReference}",
                        command.BookingReference);
                }
            }

            if (!command.OverrideTimatic)
                throw;

            _logger.LogWarning(
                "Admin check-in: Timatic override authorised for {BookingReference} — reason: {Reason}",
                command.BookingReference, command.OverrideReason);

            try
            {
                await _orderServiceClient.AddOrderNotesAsync(
                    command.BookingReference,
                    BuildOverrideNotes(command.Passengers, ticketToName, command.OverrideReason, ticketToPaxId),
                    ct);
            }
            catch (Exception notesEx)
            {
                _logger.LogError(notesEx,
                    "Admin check-in: failed to write override notes to order {BookingReference}",
                    command.BookingReference);
            }

            checkInResult = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, checkInTickets, ct, bypassTimatic: true);
        }

        if (checkInResult.TimaticNotes.Count > 0)
        {
            try
            {
                await _orderServiceClient.AddOrderNotesAsync(
                    command.BookingReference,
                    BuildOrderNotes(checkInResult.TimaticNotes, ticketToName, ticketToPaxId),
                    ct);
            }
            catch (Exception notesEx)
            {
                _logger.LogError(notesEx,
                    "Admin check-in: failed to write Timatic notes to order {BookingReference}",
                    command.BookingReference);
            }
        }

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

    private static (Dictionary<string, string> TicketToPaxId, Dictionary<string, PaxName> PaxIdToName)
        ParseOrderData(JsonElement? orderData)
    {
        var ticketToPaxId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var paxIdToName = new Dictionary<string, PaxName>(StringComparer.OrdinalIgnoreCase);

        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return (ticketToPaxId, paxIdToName);

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
                paxIdToName[pid] = new PaxName(gn, sn);
            }
        }

        if (el.TryGetProperty("eTickets", out var eTickets) &&
            eTickets.ValueKind == JsonValueKind.Array)
        {
            foreach (var et in eTickets.EnumerateArray())
            {
                var ticketNum = et.TryGetProperty("eTicketNumber", out var tnEl) ? tnEl.GetString() : null;
                var paxId = et.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
                if (ticketNum is not null && paxId is not null)
                    ticketToPaxId[ticketNum] = paxId;
            }
        }

        return (ticketToPaxId, paxIdToName);
    }

    private sealed record PaxName(string GivenName, string Surname);

    private static List<OrderTimaticNote> BuildOverrideNotes(
        IReadOnlyList<AdminCheckInPassenger> passengers,
        IReadOnlyDictionary<string, string>? ticketToName,
        string? overrideReason,
        IReadOnlyDictionary<string, string>? ticketToPaxId = null)
    {
        var reason = string.IsNullOrWhiteSpace(overrideReason) ? "No reason provided" : overrideReason;
        var timestamp = DateTime.UtcNow.ToString("o");
        return passengers.Select(p =>
        {
            var paxName = ticketToName is not null && ticketToName.TryGetValue(p.TicketNumber, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name : null;
            var subject = paxName is not null
                ? $"{paxName} (ticket {p.TicketNumber})"
                : $"ticket {p.TicketNumber}";
            var paxIdStr = ticketToPaxId is not null && ticketToPaxId.TryGetValue(p.TicketNumber, out var pid) ? pid : null;
            return new OrderTimaticNote
            {
                DateTime = timestamp,
                Type     = "TIMATIC_OVERRIDE",
                Message  = $"Timatic override by agent for {subject}: {reason}",
                PaxId    = ExtractPaxIdInt(paxIdStr)
            };
        }).ToList();
    }

    private static List<OrderTimaticNote> BuildOrderNotes(
        IReadOnlyList<OciTimaticNote> notes,
        IReadOnlyDictionary<string, string>? ticketToName = null,
        IReadOnlyDictionary<string, string>? ticketToPaxId = null)
        => notes.Select(n =>
        {
            var checkLabel = n.CheckType switch
            {
                "DOC"  => "Document check",
                "APIS" => "APIS check",
                _      => $"{n.CheckType} check"
            };
            var isFail = !string.Equals(n.Status, "PASS", StringComparison.OrdinalIgnoreCase);
            var statusText = isFail ? "failed" : "passed";
            var paxName = isFail && ticketToName is not null && ticketToName.TryGetValue(n.TicketNumber, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name : null;
            var subject = paxName is not null
                ? $"{paxName} (ticket {n.TicketNumber})"
                : $"ticket {n.TicketNumber}";
            var message = string.IsNullOrWhiteSpace(n.Detail)
                ? $"{checkLabel} {statusText} for {subject}"
                : $"{checkLabel} {statusText} for {subject}: {n.Detail}";
            var paxIdStr = ticketToPaxId is not null && ticketToPaxId.TryGetValue(n.TicketNumber, out var pid) ? pid : null;
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
