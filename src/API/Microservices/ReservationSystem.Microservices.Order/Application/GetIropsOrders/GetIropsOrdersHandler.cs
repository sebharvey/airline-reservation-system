using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Application.GetIropsOrders;

/// <summary>
/// Handles the <see cref="GetIropsOrdersQuery"/>.
/// Retrieves all confirmed orders on a flight projected for IROPS processing.
/// </summary>
public sealed class GetIropsOrdersHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetIropsOrdersHandler> _logger;

    public GetIropsOrdersHandler(
        IOrderRepository repository,
        ILogger<GetIropsOrdersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<object?>> HandleAsync(
        GetIropsOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching IROPS orders for flight {FlightNumber} on {DepartureDate}",
            query.FlightNumber, query.DepartureDate);

        var orders = await _repository.GetByFlightAsync(query.FlightNumber, query.DepartureDate, query.Status, cancellationToken);

        return orders
            .Select(o => ProjectToIropsDto(o, query.FlightNumber, query.DepartureDate))
            .Where(dto => dto is not null)
            .ToList();
    }

    internal static object? ProjectToIropsDto(
        Domain.Entities.Order order,
        string flightNumber,
        string departureDate)
    {
        if (string.IsNullOrEmpty(order.BookingReference)) return null;

        try
        {
            using var doc = JsonDocument.Parse(order.OrderData);
            var root = doc.RootElement;

            var bookingType = root.TryGetProperty("bookingType", out var bt)
                ? bt.GetString() ?? "Revenue" : "Revenue";

            // Loyalty number from first passenger
            string? loyaltyNumber = null;
            if (root.TryGetProperty("dataLists", out var dl) &&
                dl.TryGetProperty("passengers", out var paxList) &&
                paxList.ValueKind == JsonValueKind.Array)
            {
                foreach (var pax in paxList.EnumerateArray())
                {
                    if (pax.TryGetProperty("loyaltyNumber", out var ln) &&
                        ln.ValueKind != JsonValueKind.Null)
                    {
                        loyaltyNumber = ln.GetString();
                        break;
                    }
                }
            }

            // Points for reward bookings
            var totalPointsAmount = 0;
            if (root.TryGetProperty("pointsRedemption", out var pr) &&
                pr.TryGetProperty("totalPointsAmount", out var tpa))
                tpa.TryGetInt32(out totalPointsAmount);

            // Payment reference for refunds
            string? originalPaymentId = null;
            if (root.TryGetProperty("payments", out var payments) &&
                payments.ValueKind == JsonValueKind.Array)
            {
                foreach (var payment in payments.EnumerateArray())
                {
                    if (payment.TryGetProperty("paymentReference", out var payRef) &&
                        payRef.ValueKind != JsonValueKind.Null)
                    {
                        originalPaymentId = payRef.GetString();
                        break;
                    }
                }
            }

            // Find the FLIGHT order item matching the cancelled flight
            object? segment = null;
            if (root.TryGetProperty("orderItems", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("productType", out var pt) &&
                        pt.GetString() != "FLIGHT") continue;

                    var fn = item.TryGetProperty("flightNumber", out var fnEl) ? fnEl.GetString() : null;
                    var dd = item.TryGetProperty("departureDate", out var ddEl) ? ddEl.GetString() : null;

                    if (fn != flightNumber || dd != departureDate) continue;

                    var inventoryIdStr = item.TryGetProperty("inventoryId", out var invEl)
                        ? invEl.GetString() : null;
                    Guid.TryParse(inventoryIdStr, out var inventoryId);

                    segment = new
                    {
                        segmentId = item.TryGetProperty("offerId", out var oid) ? oid.GetString() ?? "" : "",
                        inventoryId,
                        flightNumber = fn,
                        departureDate = dd,
                        cabinCode = item.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "" : "",
                        origin = item.TryGetProperty("origin", out var org) ? org.GetString() ?? "" : "",
                        destination = item.TryGetProperty("destination", out var dest) ? dest.GetString() ?? "" : ""
                    };
                    break;
                }
            }

            if (segment is null) return null;

            // Passengers
            var passengers = new List<object>();
            if (root.TryGetProperty("dataLists", out var dl2) &&
                dl2.TryGetProperty("passengers", out var paxArr) &&
                paxArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var pax in paxArr.EnumerateArray())
                {
                    passengers.Add(new
                    {
                        passengerId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                        givenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                        surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                        passengerType = pax.TryGetProperty("passengerType", out var ptype)
                            ? ptype.GetString() ?? "ADT" : "ADT",
                        eTicketNumbers = Array.Empty<string>()
                    });
                }
            }

            return new
            {
                orderId = order.OrderId,
                bookingReference = order.BookingReference,
                bookingType,
                loyaltyNumber,
                loyaltyTier = (string?)null,
                bookingDate = order.CreatedAt,
                totalPaid = order.TotalAmount ?? 0m,
                totalPointsAmount,
                originalPaymentId,
                currencyCode = order.CurrencyCode,
                segment,
                passengers
            };
        }
        catch
        {
            return null;
        }
    }
}
