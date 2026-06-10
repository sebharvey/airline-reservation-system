using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Order.Application.UpdateOrderPassengers;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using OrderEntity = ReservationSystem.Microservices.Order.Domain.Entities.Order;

namespace ReservationSystem.Tests.Microservices.Order.Application;

public sealed class UpdateOrderPassengersHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly UpdateOrderPassengersHandler _handler;

    public UpdateOrderPassengersHandlerTests()
    {
        _handler = new UpdateOrderPassengersHandler(
            _orderRepository.Object,
            NullLogger<UpdateOrderPassengersHandler>.Instance);
    }

    private static OrderEntity MakeConfirmedOrder(string orderData) =>
        OrderEntity.Reconstitute(
            Guid.NewGuid(), "ABC123", "Confirmed", "WEB", "GBP",
            null, 100m, 1, orderData, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_MatchesByPaxId_UpdatesPassengerDocument()
    {
        var orderData = """
            {
              "dataLists": {
                "passengers": [
                  { "paxId": 1, "passengerId": "PAX-1", "givenName": "John", "surname": "Smith", "docs": [] }
                ]
              }
            }
            """;
        var order = MakeConfirmedOrder(orderData);

        _orderRepository
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var passengersData = """
            {
              "passengers": [
                {
                  "paxId": 1,
                  "passengerId": "PAX-1",
                  "docs": [
                    { "type": "PP", "number": "AB123456", "issuingCountry": "GBR",
                      "nationality": "GBR", "issueDate": "2020-01-01", "expiryDate": "2030-01-01" }
                  ]
                }
              ]
            }
            """;
        var command = new UpdateOrderPassengersCommand("ABC123", passengersData);

        var result = await _handler.HandleAsync(command);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result.OrderData);
        var pax = doc.RootElement.GetProperty("dataLists").GetProperty("passengers")[0];
        var docs = pax.GetProperty("docs");
        Assert.Equal(1, docs.GetArrayLength());
        Assert.Equal("AB123456", docs[0].GetProperty("number").GetString());

        _orderRepository.Verify(r => r.UpdateAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PayloadWithOnlyPassengerId_DoesNotUpdatePassenger()
    {
        // Reproduces the pre-fix bug: orchestration sent passengerId (string) but not paxId (int),
        // so the handler found no match and silently skipped the update.
        var orderData = """
            {
              "dataLists": {
                "passengers": [
                  { "paxId": 1, "passengerId": "PAX-1", "givenName": "John", "surname": "Smith", "docs": [] }
                ]
              }
            }
            """;
        var order = MakeConfirmedOrder(orderData);

        _orderRepository
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var passengersData = """
            {
              "passengers": [
                {
                  "passengerId": "PAX-1",
                  "docs": [
                    { "type": "PP", "number": "AB123456", "issuingCountry": "GBR",
                      "nationality": "GBR", "issueDate": "2020-01-01", "expiryDate": "2030-01-01" }
                  ]
                }
              ]
            }
            """;
        var command = new UpdateOrderPassengersCommand("ABC123", passengersData);

        var result = await _handler.HandleAsync(command);

        // Handler returns the original order unchanged; no UpdateAsync call made
        Assert.Equal(order, result);
        _orderRepository.Verify(r => r.UpdateAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
