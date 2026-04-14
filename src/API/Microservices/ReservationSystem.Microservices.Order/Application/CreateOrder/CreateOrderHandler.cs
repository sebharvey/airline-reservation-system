using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

/// <summary>
/// Handles the <see cref="CreateOrderCommand"/>.
/// Creates a minimal Draft order record and returns the OrderId immediately —
/// no validation is performed. The basket is left intact. All validation
/// (basket state, passenger completeness, segment completeness) is deferred
/// to <see cref="ConfirmOrder.ConfirmOrderHandler"/> as its first step.
/// </summary>
public sealed class CreateOrderHandler
{
    private readonly IBasketRepository _basketRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IBasketRepository basketRepository,
        IOrderRepository orderRepository,
        ILogger<CreateOrderHandler> logger)
    {
        _basketRepository = basketRepository;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating draft order from basket {BasketId}", command.BasketId);

        // Load basket silently to obtain currency for the Order row.
        // No validation — if the basket is missing or invalid that will be caught at confirm time.
        var basket = await _basketRepository.GetByIdAsync(command.BasketId, cancellationToken);
        var channelCode = command.ChannelCode;
        var currencyCode = basket?.CurrencyCode ?? "GBP";
        var totalAmount = basket?.TotalAmount;

        // Minimal OrderData — basket content is read and validated at confirm time
        var orderData = new JsonObject
        {
            ["basketId"] = command.BasketId.ToString(),
            ["bookingType"] = command.BookingType,
            ["history"] = new JsonArray
            {
                new JsonObject
                {
                    ["event"] = "OrderCreated",
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                }
            }
        };

        if (!string.IsNullOrEmpty(command.RedemptionReference))
        {
            orderData["pointsRedemption"] = new JsonObject
            {
                ["redemptionReference"] = command.RedemptionReference,
                ["status"] = "Pending"
            };
        }

        var order = Domain.Entities.Order.Create(
            channelCode,
            currencyCode,
            totalAmount,
            orderData.ToJsonString());

        await _orderRepository.CreateAsync(order, cancellationToken);

        _logger.LogInformation(
            "Draft order {OrderId} created for basket {BasketId}",
            order.OrderId, command.BasketId);

        return order;
    }
}
