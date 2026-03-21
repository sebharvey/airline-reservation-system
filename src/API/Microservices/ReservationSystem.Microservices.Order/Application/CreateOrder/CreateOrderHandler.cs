using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

/// <summary>
/// Handles the <see cref="CreateOrderCommand"/>.
/// Confirms a basket and creates a new order.
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
        _logger.LogInformation("Creating order from basket {BasketId}", command.BasketId);

        var basket = await _basketRepository.GetByIdAsync(command.BasketId, cancellationToken);
        if (basket is null)
        {
            throw new InvalidOperationException($"Basket {command.BasketId} not found.");
        }

        if (basket.BasketStatus != BasketStatusValues.Active)
        {
            throw new InvalidOperationException($"Basket is not open. Current status: {basket.BasketStatus}");
        }

        var bookingReference = GenerateBookingReference();

        // Build OrderData from basket data per IATA ONE Order structure
        var basketJson = JsonNode.Parse(basket.BasketData)?.AsObject() ?? new JsonObject();

        // Parse e-tickets and payment references from the command
        JsonNode? eTicketsNode = null;
        JsonNode? paymentsNode = null;
        try { eTicketsNode = JsonNode.Parse(command.ETicketsJson); } catch { }
        try { paymentsNode = JsonNode.Parse(command.PaymentReferencesJson); } catch { }

        var orderData = new JsonObject
        {
            ["dataLists"] = new JsonObject
            {
                ["passengers"] = basketJson["passengers"]?.DeepClone() ?? new JsonArray(),
                ["flightSegments"] = basketJson["flightOffers"]?.DeepClone() ?? new JsonArray()
            },
            ["orderItems"] = basketJson["flightOffers"]?.DeepClone() ?? new JsonArray(),
            ["payments"] = paymentsNode?.DeepClone() ?? new JsonArray(),
            ["eTickets"] = eTicketsNode?.DeepClone() ?? new JsonArray(),
            ["seatAssignments"] = basketJson["seats"]?.DeepClone() ?? new JsonArray(),
            ["bagItems"] = basketJson["bags"]?.DeepClone() ?? new JsonArray(),
            ["ssrItems"] = basketJson["ssrSelections"]?.DeepClone() ?? new JsonArray(),
            ["bookingType"] = command.BookingType,
            ["history"] = new JsonArray
            {
                new JsonObject
                {
                    ["event"] = "OrderConfirmed",
                    ["timestamp"] = DateTimeOffset.UtcNow.ToString("o")
                }
            }
        };

        if (!string.IsNullOrEmpty(command.RedemptionReference))
        {
            orderData["pointsRedemption"] = new JsonObject
            {
                ["redemptionReference"] = command.RedemptionReference,
                ["status"] = "Settled"
            };
        }

        var order = Domain.Entities.Order.Create(
            basket.ChannelCode,
            basket.CurrencyCode,
            basket.TotalAmount,
            orderData.ToJsonString());

        // Use Reconstitute to set the booking reference and confirmed status
        var confirmedOrder = Domain.Entities.Order.Reconstitute(
            order.OrderId,
            bookingReference,
            OrderStatusValues.Confirmed,
            order.ChannelCode,
            order.CurrencyCode,
            order.TicketingTimeLimit,
            order.TotalAmount,
            order.Version,
            order.OrderData,
            order.CreatedAt,
            order.UpdatedAt);

        await _orderRepository.CreateAsync(confirmedOrder, cancellationToken);

        // Confirm and delete the basket
        basket.Confirm(confirmedOrder.OrderId);
        await _basketRepository.UpdateAsync(basket, cancellationToken);
        await _basketRepository.DeleteAsync(basket.BasketId, cancellationToken);

        _logger.LogInformation("Order {OrderId} created with booking reference {BookingReference} from basket {BasketId}",
            confirmedOrder.OrderId, bookingReference, command.BasketId);

        return confirmedOrder;
    }

    private static string GenerateBookingReference()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
