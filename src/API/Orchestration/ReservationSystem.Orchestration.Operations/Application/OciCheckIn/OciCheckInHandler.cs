using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciCheckIn;

public sealed record OciCheckInCommand(
    string BookingReference,
    string DepartureAirport);

public sealed record OciCheckInResult(
    string BookingReference,
    IReadOnlyList<string> CheckedIn);

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

        if (tickets.Count == 0)
        {
            _logger.LogWarning("OCI check-in: no tickets found for {BookingReference}", command.BookingReference);
            return new OciCheckInResult(command.BookingReference, []);
        }

        var result = await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, tickets, ct);
        var checkedIn = result.Tickets.Select(t => t.TicketNumber).ToList();

        return new OciCheckInResult(command.BookingReference, checkedIn);
    }

    private static List<OciCheckInTicket> BuildCheckInTickets(JsonElement? orderData)
    {
        var tickets = new List<OciCheckInTicket>();
        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object) return tickets;

        // Build passengerId → name map from dataLists.passengers
        var nameMap = new Dictionary<string, (string GivenName, string Surname)>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty("dataLists", out var dl) &&
            dl.TryGetProperty("passengers", out var paxArr) &&
            paxArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var pax in paxArr.EnumerateArray())
            {
                var pid = pax.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
                var gn = pax.TryGetProperty("givenName", out var gnEl) ? gnEl.GetString() ?? "" : "";
                var sn = pax.TryGetProperty("surname", out var snEl) ? snEl.GetString() ?? "" : "";
                if (pid is not null) nameMap[pid] = (gn, sn);
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

            nameMap.TryGetValue(paxId, out var names);
            tickets.Add(new OciCheckInTicket
            {
                TicketNumber = ticketNum,
                PassengerId = paxId,
                GivenName = names.GivenName ?? "",
                Surname = names.Surname ?? ""
            });
        }

        return tickets;
    }
}
