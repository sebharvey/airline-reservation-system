using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Tests.Microservices.Order.Application;

public sealed class CreateOrderHandlerTests
{
    private readonly Mock<IBasketRepository> _basketRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _handler = new CreateOrderHandler(
            _basketRepository.Object,
            _orderRepository.Object,
            NullLogger<CreateOrderHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithValidBasket_CreatesOrderWithBasketCurrency()
    {
        // Arrange
        var basketId = Guid.NewGuid();
        var basket = Basket.Create("USD", DateTime.UtcNow.AddHours(2));
        var command = new CreateOrderCommand(basketId, "WEB", null, "Revenue");

        _basketRepository
            .Setup(r => r.GetByIdAsync(basketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(basket);

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USD", result.CurrencyCode);
        Assert.Equal("WEB", result.ChannelCode);
        Assert.Equal("Draft", result.OrderStatus);

        _orderRepository.Verify(
            r => r.CreateAsync(It.IsAny<Domain.Entities.Order>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenBasketNotFound_DefaultsToGbpCurrency()
    {
        // Arrange
        var basketId = Guid.NewGuid();
        var command = new CreateOrderCommand(basketId, "AGENT", null, "Revenue");

        _basketRepository
            .Setup(r => r.GetByIdAsync(basketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Basket?)null);

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GBP", result.CurrencyCode);
        Assert.Equal("AGENT", result.ChannelCode);
    }
}
