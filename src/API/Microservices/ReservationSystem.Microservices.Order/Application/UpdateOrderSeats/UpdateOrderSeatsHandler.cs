using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;

/// <summary>
/// Handles the <see cref="UpdateOrderSeatsCommand"/>.
/// Updates seat assignments on a confirmed order post-booking.
/// </summary>
public sealed class UpdateOrderSeatsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderSeatsHandler> _logger;

    public UpdateOrderSeatsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderSeatsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderSeatsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating seats for order {BookingReference}", command.BookingReference);

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
        var seatsNode = JsonNode.Parse(command.SeatsData);
        orderJson["seats"] = seatsNode;

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

        _logger.LogInformation("Order {BookingReference} seats updated", command.BookingReference);

        return updated;
    }
}
