using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciSeats;

public sealed record OciSeatSelection(string PassengerId, string SegmentRef, string SeatNumber);

public sealed record OciSeatsCommand(
    string BookingReference,
    IReadOnlyList<OciSeatSelection> Seats);

public sealed record OciSeatAssignment(
    string PassengerId,
    string SegmentRef,
    string SeatNumber,
    bool Updated);

public sealed record OciSeatsResult(
    string BookingReference,
    IReadOnlyList<OciSeatAssignment> Assignments);

public sealed class OciSeatsHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<OciSeatsHandler> _logger;

    public OciSeatsHandler(
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<OciSeatsHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<OciSeatsResult?> HandleAsync(OciSeatsCommand command, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);
        if (order is null) return null;

        var paxToTicket = CheckInHelper.ParsePaxToTicketMap(order.OrderData);
        var segmentInventoryMap = CheckInHelper.ParseSegmentInventoryMap(order.OrderData);
        var currency = order.CurrencyCode;

        var assignments = new List<OciSeatAssignment>();
        var seatsPayload = new List<object>();

        foreach (var selection in command.Seats)
        {
            if (!paxToTicket.TryGetValue(selection.PassengerId, out var ticketNumber))
            {
                _logger.LogWarning(
                    "OCI seats: passenger {PassengerId} not found on {BookingReference}",
                    selection.PassengerId, command.BookingReference);
                assignments.Add(new OciSeatAssignment(selection.PassengerId, selection.SegmentRef, selection.SeatNumber, false));
                continue;
            }

            if (!segmentInventoryMap.TryGetValue(selection.SegmentRef, out var inventoryId))
            {
                _logger.LogWarning(
                    "OCI seats: segment {SegmentRef} not found on {BookingReference}",
                    selection.SegmentRef, command.BookingReference);
                assignments.Add(new OciSeatAssignment(selection.PassengerId, selection.SegmentRef, selection.SeatNumber, false));
                continue;
            }

            var updated = await _deliveryServiceClient.UpdateManifestSeatAsync(
                ticketNumber, inventoryId, selection.SeatNumber, ct);

            if (!updated)
                _logger.LogWarning(
                    "OCI seats: manifest entry not found for ticket {TicketNumber} on {BookingReference}",
                    ticketNumber, command.BookingReference);

            assignments.Add(new OciSeatAssignment(
                selection.PassengerId, selection.SegmentRef, selection.SeatNumber, updated));

            seatsPayload.Add(new
            {
                passengerId = selection.PassengerId,
                segmentId   = selection.SegmentRef,
                seatNumber  = selection.SeatNumber,
                price       = 0m,
                tax         = 0m,
                currency
            });
        }

        if (seatsPayload.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(
                    command.BookingReference, seatsPayload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OCI seats: failed to persist seat assignments to order {BookingReference}",
                    command.BookingReference);
                // Non-fatal: manifest is the authoritative departure record.
            }
        }

        return new OciSeatsResult(command.BookingReference, assignments);
    }
}
