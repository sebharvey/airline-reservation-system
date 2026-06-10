using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Order.Application.GetIropsOrders;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using OrderEntity = ReservationSystem.Microservices.Order.Domain.Entities.Order;

namespace ReservationSystem.Tests.Microservices.Order.Application;

public sealed class GetIropsOrdersHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly GetIropsOrdersHandler _handler;

    public GetIropsOrdersHandlerTests()
    {
        _handler = new GetIropsOrdersHandler(
            _orderRepository.Object,
            NullLogger<GetIropsOrdersHandler>.Instance);
    }

    private static OrderEntity MakeConfirmedOrder(string orderData) =>
        OrderEntity.Reconstitute(
            Guid.NewGuid(), "ABC123", "Confirmed", "WEB", "GBP",
            null, 100m, 1, orderData, DateTime.UtcNow, DateTime.UtcNow);

    private void SetupRepository(OrderEntity order)
    {
        _orderRepository
            .Setup(r => r.GetByFlightAsync("AA101", "2026-07-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);
    }

    private static string MakeOrderData(string passengersJson) => $$"""
        {
          "dataLists": { "passengers": {{passengersJson}} },
          "orderItems": [
            { "productType": "FLIGHT", "offerId": "OFFER-1",
              "inventoryId": "7e6c1d2a-0000-0000-0000-000000000001",
              "flightNumber": "AA101", "departureDate": "2026-07-01",
              "cabinCode": "Y", "origin": "LHR", "destination": "JFK" }
          ]
        }
        """;

    private static JsonElement GetSinglePassenger(IReadOnlyList<object?> result)
    {
        var json = JsonSerializer.Serialize(Assert.Single(result));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("passengers").EnumerateArray().Single().Clone();
    }

    [Fact]
    public async Task HandleAsync_PassengerWithIntegerPaxId_DerivesPassengerIdFromPaxId()
    {
        // The integer paxId is the primary identifier — the legacy passengerId string is ignored.
        var order = MakeConfirmedOrder(MakeOrderData(
            """[{ "paxId": 2, "passengerId": "PAX-STALE", "givenName": "Ada", "surname": "Lovelace", "passengerType": "ADT" }]"""));
        SetupRepository(order);

        var result = await _handler.HandleAsync(new GetIropsOrdersQuery("AA101", "2026-07-01", null));

        var pax = GetSinglePassenger(result);
        Assert.Equal("PAX-2", pax.GetProperty("passengerId").GetString());
    }

    [Fact]
    public async Task HandleAsync_PassengerWithoutPaxId_FallsBackToLegacyPassengerId()
    {
        // Older order data has no paxId — the legacy passengerId string is passed through.
        var order = MakeConfirmedOrder(MakeOrderData(
            """[{ "passengerId": "PAX-1", "givenName": "Ada", "surname": "Lovelace", "passengerType": "ADT" }]"""));
        SetupRepository(order);

        var result = await _handler.HandleAsync(new GetIropsOrdersQuery("AA101", "2026-07-01", null));

        var pax = GetSinglePassenger(result);
        Assert.Equal("PAX-1", pax.GetProperty("passengerId").GetString());
    }
}
