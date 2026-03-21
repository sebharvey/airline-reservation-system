using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.RebookOrder;

/// <summary>
/// Handles the <see cref="RebookOrderCommand"/>.
/// Rebooks a passenger under IROPS onto an alternative flight.
/// </summary>
public sealed class RebookOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<RebookOrderHandler> _logger;

    public RebookOrderHandler(
        IOrderRepository repository,
        ILogger<RebookOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        RebookOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rebooking order {BookingReference}", command.BookingReference);

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
        var rebookNode = JsonNode.Parse(command.RebookData)?.AsObject();

        if (rebookNode is not null)
        {
            // Add rebook details to the order
            orderJson["rebookDetails"] = rebookNode.DeepClone();
            orderJson["rebookedAt"] = DateTimeOffset.UtcNow.ToString("o");
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

        _logger.LogInformation("Order {BookingReference} rebooked, version={Version}",
            command.BookingReference, updated.Version);

        return updated;
    }
}
