using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.ChangeOrder;

/// <summary>
/// Handles the <see cref="ChangeOrderCommand"/>.
/// Changes flight details on a confirmed order (voluntary change).
/// </summary>
public sealed class ChangeOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<ChangeOrderHandler> _logger;

    public ChangeOrderHandler(
        IOrderRepository repository,
        ILogger<ChangeOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        ChangeOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Changing order {BookingReference}", command.BookingReference);

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
        var changeNode = JsonNode.Parse(command.ChangeData)?.AsObject();

        if (changeNode is not null)
        {
            // Add the new segment data to the order
            orderJson["newSegment"] = changeNode.DeepClone();
            orderJson["lastChangedAt"] = DateTimeOffset.UtcNow.ToString("o");
        }

        var updated = Domain.Entities.Order.Reconstitute(
            order.OrderId,
            order.BookingReference,
            OrderStatusValues.Changed,
            order.ChannelCode,
            order.CurrencyCode,
            order.TicketingTimeLimit,
            order.TotalAmount,
            order.Version + 1,
            orderJson.ToJsonString(),
            order.CreatedAt,
            DateTimeOffset.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Order {BookingReference} changed, version={Version}",
            command.BookingReference, updated.Version);

        return updated;
    }
}
