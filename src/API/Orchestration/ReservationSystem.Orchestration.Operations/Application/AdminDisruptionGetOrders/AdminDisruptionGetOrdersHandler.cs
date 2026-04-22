using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionGetOrders;

public sealed class AdminDisruptionGetOrdersHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<AdminDisruptionGetOrdersHandler> _logger;

    public AdminDisruptionGetOrdersHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        ILogger<AdminDisruptionGetOrdersHandler> logger)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    public async Task<AdminDisruptionOrdersResponse> HandleAsync(
        AdminDisruptionGetOrdersQuery query,
        CancellationToken ct)
    {
        var flightInventory = await _offerServiceClient.GetFlightInventoryAsync(query.FlightNumber, query.DepartureDate, ct);
        if (flightInventory is null)
            throw new KeyNotFoundException($"Flight {query.FlightNumber} on {query.DepartureDate} not found.");

        // Use inventory holds as the source of truth — the same data source the holds modal uses.
        var holds = await _offerServiceClient.GetInventoryHoldsAsync(flightInventory.InventoryId, ct);

        // Only Revenue holds with a known order. Standby and unlinked holds are excluded.
        var revenueHolds = holds
            .Where(h => h.HoldType == "Revenue" && h.OrderId != Guid.Empty)
            .ToList();

        if (revenueHolds.Count == 0)
        {
            return new AdminDisruptionOrdersResponse
            {
                FlightNumber = query.FlightNumber,
                DepartureDate = query.DepartureDate,
                Origin = flightInventory.Origin,
                Destination = flightInventory.Destination,
                Orders = []
            };
        }

        // Deduplicate: one entry per orderId, keeping first hold's cabin code.
        var holdsByOrderId = revenueHolds
            .GroupBy(h => h.OrderId)
            .ToDictionary(g => g.Key, g => g.First());

        var orderIds = holdsByOrderId.Keys.ToList();

        var affectedOrders = await _orderServiceClient.GetAffectedOrdersByIdsAsync(
            orderIds, query.FlightNumber, query.DepartureDate, ct);

        // Index order details by orderId for O(1) join.
        var orderDetailsByOrderId = affectedOrders.Orders
            .ToDictionary(o => o.OrderId);

        var items = holdsByOrderId
            .Select(kvp =>
            {
                var hold = kvp.Value;
                var orderId = kvp.Key;

                if (!orderDetailsByOrderId.TryGetValue(orderId, out var order))
                {
                    // Hold exists but order details unavailable — surface with minimal data.
                    return new IropsOrderItem
                    {
                        BookingReference = hold.BookingReference ?? string.Empty,
                        BookingType = "Revenue",
                        CabinCode = hold.CabinCode,
                        BookingDate = DateTime.MinValue,
                        PassengerCount = 1,
                        PassengerNames = hold.PassengerName is not null ? [hold.PassengerName] : []
                    };
                }

                return new IropsOrderItem
                {
                    BookingReference = order.BookingReference,
                    BookingType = order.BookingType,
                    CabinCode = order.Segment.CabinCode,
                    LoyaltyTier = order.LoyaltyTier,
                    LoyaltyNumber = order.LoyaltyNumber,
                    BookingDate = order.BookingDate,
                    PassengerCount = order.Passengers.Count,
                    PassengerNames = order.Passengers
                        .Select(p => $"{p.GivenName} {p.Surname}".Trim())
                        .ToList()
                };
            })
            .OrderBy(o => CabinPriority(o.CabinCode))
            .ThenBy(o => LoyaltyTierPriority(o.LoyaltyTier))
            .ThenBy(o => o.BookingDate)
            .ToList();

        _logger.LogInformation(
            "Returning {Count} affected order(s) for {FlightNumber} on {DepartureDate}",
            items.Count, query.FlightNumber, query.DepartureDate);

        return new AdminDisruptionOrdersResponse
        {
            FlightNumber = query.FlightNumber,
            DepartureDate = query.DepartureDate,
            Origin = flightInventory.Origin,
            Destination = flightInventory.Destination,
            Orders = items
        };
    }

    private static int CabinPriority(string cabinCode) => cabinCode switch
    {
        "F" => 0,
        "J" => 1,
        "W" => 2,
        "Y" => 3,
        _ => 4
    };

    private static int LoyaltyTierPriority(string? tier) => tier switch
    {
        "Platinum" => 0,
        "Gold" => 1,
        "Silver" => 2,
        "Blue" => 3,
        _ => 4
    };
}
