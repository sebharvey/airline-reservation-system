using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.ConfirmOrder;

/// <summary>
/// Handles the <see cref="ConfirmOrderCommand"/>.
/// Validates a draft order has all required data, assigns a booking reference,
/// transitions it to Confirmed, and deletes the originating basket.
/// </summary>
public sealed class ConfirmOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBasketRepository _basketRepository;
    private readonly ILogger<ConfirmOrderHandler> _logger;

    public ConfirmOrderHandler(
        IOrderRepository orderRepository,
        IBasketRepository basketRepository,
        ILogger<ConfirmOrderHandler> logger)
    {
        _orderRepository = orderRepository;
        _basketRepository = basketRepository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order> HandleAsync(
        ConfirmOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming order {OrderId}", command.OrderId);

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {command.OrderId} not found.");

        if (order.OrderStatus != OrderStatusValues.Draft)
            throw new InvalidOperationException(
                $"Order cannot be confirmed. Current status: {order.OrderStatus}");

        var basket = await _basketRepository.GetByIdAsync(command.BasketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Basket {command.BasketId} not found.");

        if (!basket.IsActive)
            throw new InvalidOperationException(
                $"Basket is no longer active. Current status: {basket.BasketStatus}");

        // Validate the order has the minimum required data
        var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var dataLists = orderJson["dataLists"]?.AsObject();
        var passengers = dataLists?["passengers"]?.AsArray();
        var segments = dataLists?["flightSegments"]?.AsArray();

        if (passengers is null || passengers.Count == 0)
            throw new InvalidOperationException("Order cannot be confirmed: no passengers present.");

        if (segments is null || segments.Count == 0)
            throw new InvalidOperationException("Order cannot be confirmed: no flight segments present.");

        // Merge payment references into order data
        JsonNode? paymentsNode = null;
        try { paymentsNode = JsonNode.Parse(command.PaymentReferencesJson); } catch { }
        orderJson["payments"] = paymentsNode?.DeepClone() ?? new JsonArray();

        // Append OrderConfirmed history event
        if (orderJson["history"] is not JsonArray history)
        {
            history = new JsonArray();
            orderJson["history"] = history;
        }
        history.Add(new JsonObject
        {
            ["event"] = "OrderConfirmed",
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        });

        var bookingReference = GenerateBookingReference();

        order.Confirm(
            bookingReference,
            order.TotalAmount ?? 0m,
            orderJson.ToJsonString(),
            basket.ExpiresAt);

        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Mark basket as confirmed then delete it
        basket.Confirm(order.OrderId);
        await _basketRepository.UpdateAsync(basket, cancellationToken);
        await _basketRepository.DeleteAsync(basket.BasketId, cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} confirmed with booking reference {BookingReference}",
            order.OrderId, bookingReference);

        return order;
    }

    private static string GenerateBookingReference()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
