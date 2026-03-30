using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

/// <summary>
/// Handles the <see cref="CreateOrderCommand"/>.
/// Creates a Draft order record from a basket. The basket is left intact so
/// that PATCH operations can be applied before the order is confirmed via
/// <see cref="ConfirmOrder.ConfirmOrderHandler"/>.
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

        var basket = await _basketRepository.GetByIdAsync(command.BasketId, cancellationToken);
        if (basket is null)
            throw new InvalidOperationException($"Basket {command.BasketId} not found.");

        if (basket.BasketStatus != BasketStatusValues.Active)
            throw new InvalidOperationException($"Basket is not active. Current status: {basket.BasketStatus}");

        // Build OrderData from basket — payment references are empty until the order is confirmed
        var basketJson = JsonNode.Parse(basket.BasketData)?.AsObject() ?? new JsonObject();

        var orderData = new JsonObject
        {
            ["dataLists"] = new JsonObject
            {
                ["passengers"] = basketJson["passengers"]?.DeepClone() ?? new JsonArray(),
                ["flightSegments"] = basketJson["flightOffers"]?.DeepClone() ?? new JsonArray()
            },
            ["orderItems"] = basketJson["flightOffers"]?.DeepClone() ?? new JsonArray(),
            ["payments"] = new JsonArray(),
            ["eTickets"] = new JsonArray(),
            ["seatAssignments"] = basketJson["seats"]?.DeepClone() ?? new JsonArray(),
            ["bagItems"] = basketJson["bags"]?.DeepClone() ?? new JsonArray(),
            ["ssrItems"] = basketJson["ssrSelections"]?.DeepClone() ?? new JsonArray(),
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
            basket.ChannelCode,
            basket.CurrencyCode,
            basket.TotalAmount,
            orderData.ToJsonString());

        await _orderRepository.CreateAsync(order, cancellationToken);

        _logger.LogInformation(
            "Draft order {OrderId} created from basket {BasketId}",
            order.OrderId, command.BasketId);

        return order;
    }
}
