using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderBags;

/// <summary>
/// Handles the <see cref="UpdateOrderBagsCommand"/>.
/// Adds bag ancillaries to a confirmed order post-booking.
/// </summary>
public sealed class UpdateOrderBagsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderBagsHandler> _logger;

    public UpdateOrderBagsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderBagsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderBagsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating bags for order {BookingReference}", command.BookingReference);

        var order = await _repository.GetByBookingReferenceAsync(command.BookingReference, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {BookingReference} not found", command.BookingReference);
            return null;
        }

        if (order.OrderStatus != OrderStatusValues.Confirmed && order.OrderStatus != OrderStatusValues.Changed)
        {
            _logger.LogWarning("Order {BookingReference} is not mutable (status: {Status})",
                command.BookingReference, order.OrderStatus);
            throw new InvalidOperationException($"Order is not mutable. Current status: {order.OrderStatus}");
        }

        var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var bagsNode = JsonNode.Parse(command.BagsData);

        // Build new BAG items from the incoming payload
        var newBagItems = new List<JsonObject>();
        if (bagsNode is JsonArray bagsArray)
        {
            foreach (var bag in bagsArray)
            {
                if (bag is not JsonObject bagObj) continue;
                newBagItems.Add(new JsonObject
                {
                    ["productType"]    = "BAG",
                    ["passengerId"]    = bagObj["passengerId"]?.GetValue<string>(),
                    ["segmentId"]      = bagObj["segmentId"]?.GetValue<string>(),
                    ["additionalBags"] = bagObj["additionalBags"]?.DeepClone(),
                    ["bagOfferId"]     = bagObj["bagOfferId"]?.DeepClone(),
                    ["price"]          = bagObj["price"]?.DeepClone(),
                    ["tax"]            = bagObj["tax"]?.DeepClone(),
                    ["currency"]       = bagObj["currency"]?.DeepClone(),
                    ["paymentReference"] = bagObj["paymentReference"]?.GetValue<string>(),
                });
            }
        }

        // Rebuild orderItems: keep non-BAG items, append new BAG items
        var updatedOrderItems = new JsonArray();
        if (orderJson["orderItems"] is JsonArray existingOrderItems)
        {
            foreach (var item in existingOrderItems)
            {
                if (item is not JsonObject obj) continue;
                var pt = obj["productType"]?.GetValue<string>();
                if (!string.Equals(pt, "BAG", StringComparison.OrdinalIgnoreCase))
                    updatedOrderItems.Add(obj.DeepClone());
            }
        }
        foreach (var bagItem in newBagItems)
            updatedOrderItems.Add(bagItem);

        orderJson["orderItems"] = updatedOrderItems;
        orderJson.Remove("bags");

        var updated = Domain.Entities.Order.Reconstitute(
            order.OrderId,
            order.BookingReference,
            order.OrderStatus,
            order.ChannelCode,
            order.CurrencyCode,
            order.TicketingTimeLimit,
            order.TotalAmount,
            order.Version + 1,
            orderJson.ToJsonString(),
            order.CreatedAt,
            DateTime.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Order {BookingReference} bags updated", command.BookingReference);

        return updated;
    }
}
