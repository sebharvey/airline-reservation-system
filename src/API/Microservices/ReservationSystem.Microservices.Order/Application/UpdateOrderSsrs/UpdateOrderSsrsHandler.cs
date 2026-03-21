using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;

/// <summary>
/// Handles the <see cref="UpdateOrderSsrsCommand"/>.
/// Updates Special Service Requests on a confirmed order.
/// </summary>
public sealed class UpdateOrderSsrsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderSsrsHandler> _logger;

    public UpdateOrderSsrsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderSsrsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderSsrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating SSRs for order {BookingReference}", command.BookingReference);

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
        var ssrsNode = JsonNode.Parse(command.SsrsData);
        orderJson["ssrs"] = ssrsNode;

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
            DateTimeOffset.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Order {BookingReference} SSRs updated", command.BookingReference);

        return updated;
    }
}
