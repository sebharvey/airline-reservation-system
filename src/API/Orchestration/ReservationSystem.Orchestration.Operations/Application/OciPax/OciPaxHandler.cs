using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciPax;

public sealed record OciPaxTravelDocument(
    string Type,
    string Number,
    string IssuingCountry,
    string Nationality,
    string IssueDate,
    string ExpiryDate);

public sealed record OciPaxPassenger(string TicketNumber, OciPaxTravelDocument TravelDocument);

public sealed record OciPaxCommand(
    string BookingReference,
    string DepartureAirport,
    IReadOnlyList<OciPaxPassenger> Passengers);

public sealed record OciPaxResult(
    string BookingReference,
    bool Success);

public sealed class OciPaxHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<OciPaxHandler> _logger;

    public OciPaxHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<OciPaxHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<OciPaxResult?> HandleAsync(OciPaxCommand command, CancellationToken ct)
    {
        // Retrieve current order to map ticketNumber → passengerId
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);

        if (order is null)
        {
            _logger.LogWarning("OCI pax: order not found for {BookingReference}", command.BookingReference);
            return null;
        }

        // Build ticketNumber → passengerId map from eTickets in orderData
        var ticketToPax = BuildTicketToPaxMap(order.OrderData);
        var paxToDetails = BuildPaxDetailsMap(order.OrderData);

        // Build passenger update payload with passengerId (required by Order MS)
        var passengerUpdates = new List<object>();
        var checkInTickets = new List<OciCheckInTicket>();

        foreach (var paxRequest in command.Passengers)
        {
            if (!ticketToPax.TryGetValue(paxRequest.TicketNumber, out var passengerId))
            {
                _logger.LogWarning("OCI pax: no passengerId found for ticket {TicketNumber}", paxRequest.TicketNumber);
                continue;
            }

            paxToDetails.TryGetValue(passengerId, out var details);

            passengerUpdates.Add(new
            {
                passengerId,
                travelDocument = new
                {
                    type = paxRequest.TravelDocument.Type,
                    number = paxRequest.TravelDocument.Number,
                    issuingCountry = paxRequest.TravelDocument.IssuingCountry,
                    nationality = paxRequest.TravelDocument.Nationality,
                    issueDate = paxRequest.TravelDocument.IssueDate,
                    expiryDate = paxRequest.TravelDocument.ExpiryDate
                }
            });

            checkInTickets.Add(new OciCheckInTicket
            {
                TicketNumber = paxRequest.TicketNumber,
                PassengerId = passengerId,
                GivenName = details?.GivenName ?? "",
                Surname = details?.SurnameValue ?? ""
            });
        }

        // Persist travel documents to the order
        if (passengerUpdates.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderPassengersAsync(
                    command.BookingReference,
                    new { passengers = passengerUpdates },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update passengers for order {BookingReference}", command.BookingReference);
                return new OciPaxResult(command.BookingReference, false);
            }
        }

        // Perform check-in with Delivery MS
        if (checkInTickets.Count > 0 && !string.IsNullOrWhiteSpace(command.DepartureAirport))
        {
            try
            {
                await _deliveryServiceClient.CheckInAsync(command.DepartureAirport, checkInTickets, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCI check-in failed for {BookingReference}", command.BookingReference);
                // Non-fatal: travel docs were saved; return success regardless
            }
        }

        return new OciPaxResult(command.BookingReference, true);
    }

    private static Dictionary<string, string> BuildTicketToPaxMap(JsonElement? orderData)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object) return map;
        if (!el.TryGetProperty("eTickets", out var eTickets) || eTickets.ValueKind != JsonValueKind.Array) return map;

        foreach (var et in eTickets.EnumerateArray())
        {
            var paxId = et.TryGetProperty("passengerId", out var pEl) ? pEl.GetString() : null;
            var ticketNum = et.TryGetProperty("eTicketNumber", out var tEl) ? tEl.GetString() : null;
            if (paxId is not null && ticketNum is not null)
                map[ticketNum] = paxId;
        }
        return map;
    }

    private static Dictionary<string, PaxDetails> BuildPaxDetailsMap(JsonElement? orderData)
    {
        var map = new Dictionary<string, PaxDetails>(StringComparer.OrdinalIgnoreCase);
        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object) return map;
        if (!el.TryGetProperty("dataLists", out var dl) || dl.ValueKind != JsonValueKind.Object) return map;
        if (!dl.TryGetProperty("passengers", out var paxArr) || paxArr.ValueKind != JsonValueKind.Array) return map;

        foreach (var pax in paxArr.EnumerateArray())
        {
            var paxId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
            var givenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "";
            var surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "";
            if (paxId is not null)
                map[paxId] = new PaxDetails(givenName, surname);
        }
        return map;
    }

    private sealed record PaxDetails(string GivenName, string SurnameValue);
}
