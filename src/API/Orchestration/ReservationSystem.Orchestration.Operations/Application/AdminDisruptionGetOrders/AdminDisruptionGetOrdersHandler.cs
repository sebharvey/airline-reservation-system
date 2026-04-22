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
        // Fetch flight info and confirmed orders in parallel.
        var flightsTask = _offerServiceClient.GetFlightsByDateAsync(query.DepartureDate, ct);
        var ordersTask = _orderServiceClient.GetOrdersByFlightAsync(
            query.FlightNumber, query.DepartureDate, "Confirmed", ct);

        await Task.WhenAll(flightsTask, ordersTask);

        var flightInfo = flightsTask.Result.FirstOrDefault(f => f.FlightNumber == query.FlightNumber);
        if (flightInfo is null)
            throw new KeyNotFoundException($"Flight {query.FlightNumber} on {query.DepartureDate} not found.");

        var sorted = ordersTask.Result.Orders
            .OrderBy(o => CabinPriority(o.Segment.CabinCode))
            .ThenBy(o => LoyaltyTierPriority(o.LoyaltyTier))
            .ThenBy(o => o.BookingDate)
            .Select(o => new IropsOrderItem
            {
                BookingReference = o.BookingReference,
                BookingType = o.BookingType,
                CabinCode = o.Segment.CabinCode,
                LoyaltyTier = o.LoyaltyTier,
                LoyaltyNumber = o.LoyaltyNumber,
                BookingDate = o.BookingDate,
                PassengerCount = o.Passengers.Count,
                PassengerNames = o.Passengers
                    .Select(p => $"{p.GivenName} {p.Surname}".Trim())
                    .ToList()
            })
            .ToList();

        _logger.LogInformation(
            "Returning {Count} confirmed order(s) for {FlightNumber} on {DepartureDate}",
            sorted.Count, query.FlightNumber, query.DepartureDate);

        return new AdminDisruptionOrdersResponse
        {
            FlightNumber = query.FlightNumber,
            DepartureDate = query.DepartureDate,
            Origin = flightInfo.Origin,
            Destination = flightInfo.Destination,
            Orders = sorted
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
