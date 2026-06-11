using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Order.Application.UpdateOrderBags;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using OrderEntity = ReservationSystem.Microservices.Order.Domain.Entities.Order;

namespace ReservationSystem.Tests.Microservices.Order.Application;

public sealed class UpdateOrderBagsHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly UpdateOrderBagsHandler _handler;

    public UpdateOrderBagsHandlerTests()
    {
        _handler = new UpdateOrderBagsHandler(
            _orderRepository.Object,
            NullLogger<UpdateOrderBagsHandler>.Instance);
    }

    private static OrderEntity MakeConfirmedOrder(string orderData) =>
        OrderEntity.Reconstitute(
            Guid.NewGuid(), "ABC123", "Confirmed", "WEB", "GBP",
            null, 100m, 1, orderData, DateTime.UtcNow, DateTime.UtcNow);

    private void SetupRepository(OrderEntity order)
    {
        _orderRepository
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task HandleAsync_BagWithIntegerPaxId_WritesPaxIdAndDerivedPassengerId()
    {
        // New format: caller sends integer paxId — it becomes the primary identifier.
        var order = MakeConfirmedOrder("""{"orderItems":[]}""");
        SetupRepository(order);

        var bagsData = """
            [
              { "paxId": 2, "segmentId": "SEG-1", "additionalBags": 1,
                "price": 25.00, "tax": 2.50, "currency": "GBP" }
            ]
            """;
        var result = await _handler.HandleAsync(new UpdateOrderBagsCommand("ABC123", bagsData));

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var bag = doc.RootElement.GetProperty("orderItems").EnumerateArray()
            .Single(i => i.GetProperty("productType").GetString() == "BAG");

        Assert.Equal(2, bag.GetProperty("paxId").GetInt32());
        Assert.Equal("PAX-2", bag.GetProperty("passengerId").GetString());
    }

    [Fact]
    public async Task HandleAsync_BagWithoutPaxId_StoresNoPaxId()
    {
        // paxId is always expected going forward; bags missing paxId are stored without it.
        var order = MakeConfirmedOrder("""{"orderItems":[]}""");
        SetupRepository(order);

        var bagsData = """
            [
              { "passengerId": "PAX-1", "segmentId": "SEG-1", "additionalBags": 1,
                "price": 25.00, "tax": 2.50, "currency": "GBP" }
            ]
            """;
        var result = await _handler.HandleAsync(new UpdateOrderBagsCommand("ABC123", bagsData));

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var bag = doc.RootElement.GetProperty("orderItems").EnumerateArray()
            .Single(i => i.GetProperty("productType").GetString() == "BAG");

        Assert.False(bag.TryGetProperty("paxId", out _));
    }

    [Fact]
    public async Task HandleAsync_BagWithBothPaxIdAndPassengerId_PrefersIntegerPaxId()
    {
        // When both fields are present, the integer paxId takes precedence and
        // passengerId in the stored item is derived from that integer.
        var order = MakeConfirmedOrder("""{"orderItems":[]}""");
        SetupRepository(order);

        var bagsData = """
            [
              { "paxId": 3, "passengerId": "PAX-1", "segmentId": "SEG-1",
                "additionalBags": 1, "price": 25.00, "tax": 2.50, "currency": "GBP" }
            ]
            """;
        var result = await _handler.HandleAsync(new UpdateOrderBagsCommand("ABC123", bagsData));

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var bag = doc.RootElement.GetProperty("orderItems").EnumerateArray()
            .Single(i => i.GetProperty("productType").GetString() == "BAG");

        Assert.Equal(3, bag.GetProperty("paxId").GetInt32());
        Assert.Equal("PAX-3", bag.GetProperty("passengerId").GetString());
    }
}
