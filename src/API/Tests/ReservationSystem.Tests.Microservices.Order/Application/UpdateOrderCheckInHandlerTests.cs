using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using OrderEntity = ReservationSystem.Microservices.Order.Domain.Entities.Order;

namespace ReservationSystem.Tests.Microservices.Order.Application;

public sealed class UpdateOrderCheckInHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly UpdateOrderCheckInHandler _handler;

    public UpdateOrderCheckInHandlerTests()
    {
        _handler = new UpdateOrderCheckInHandler(
            _orderRepository.Object,
            NullLogger<UpdateOrderCheckInHandler>.Instance);
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
    public async Task HandleAsync_CheckInPassengerNode_WritesBothPaxIdAndDerivedPassengerId()
    {
        // The checkin.passengers array must now include the integer paxId alongside the
        // legacy passengerId string so downstream consumers can key off either form.
        var orderData = """
            {
              "orderItems": [{ "productType": "FLIGHT", "origin": "LHR", "destination": "JFK" }],
              "dataLists": { "passengers": [] }
            }
            """;
        var order = MakeConfirmedOrder(orderData);
        SetupRepository(order);

        var command = new UpdateOrderCheckInCommand(
            BookingReference: "ABC123",
            DepartureAirport: "LHR",
            CheckedInAt: "2026-06-10T08:00:00Z",
            Passengers: new[]
            {
                new UpdateOrderCheckInPassenger(PaxId: 1, TicketNumber: "125-1234567890",
                    Status: "CheckedIn", Message: "")
            });

        var result = await _handler.HandleAsync(command);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var paxNode = doc.RootElement
            .GetProperty("orderItems")[0]
            .GetProperty("checkin")
            .GetProperty("passengers")[0];

        Assert.Equal(1, paxNode.GetProperty("paxId").GetInt32());
        Assert.Equal("PAX-1", paxNode.GetProperty("passengerId").GetString());
    }

    [Fact]
    public async Task HandleAsync_MultiplePassengers_EachNodeHasCorrectPaxId()
    {
        var orderData = """
            {
              "orderItems": [{ "productType": "FLIGHT", "origin": "LHR", "destination": "JFK" }],
              "dataLists": { "passengers": [] }
            }
            """;
        var order = MakeConfirmedOrder(orderData);
        SetupRepository(order);

        var command = new UpdateOrderCheckInCommand(
            BookingReference: "ABC123",
            DepartureAirport: "LHR",
            CheckedInAt: "2026-06-10T08:00:00Z",
            Passengers: new[]
            {
                new UpdateOrderCheckInPassenger(PaxId: 1, TicketNumber: "125-1234567890",
                    Status: "CheckedIn", Message: ""),
                new UpdateOrderCheckInPassenger(PaxId: 2, TicketNumber: "125-0987654321",
                    Status: "CheckedIn", Message: "")
            });

        var result = await _handler.HandleAsync(command);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var passengers = doc.RootElement
            .GetProperty("orderItems")[0]
            .GetProperty("checkin")
            .GetProperty("passengers")
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, passengers.Count);
        Assert.Equal(1, passengers[0].GetProperty("paxId").GetInt32());
        Assert.Equal("PAX-1", passengers[0].GetProperty("passengerId").GetString());
        Assert.Equal(2, passengers[1].GetProperty("paxId").GetInt32());
        Assert.Equal("PAX-2", passengers[1].GetProperty("passengerId").GetString());
    }
}
