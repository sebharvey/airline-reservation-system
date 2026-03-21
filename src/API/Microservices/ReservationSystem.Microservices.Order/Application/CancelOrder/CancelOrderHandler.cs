using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CancelOrder;

/// <summary>
/// Handles the <see cref="CancelOrderCommand"/>.
/// Cancels a confirmed order.
/// </summary>
public sealed class CancelOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CancelOrderHandler> _logger;

    public CancelOrderHandler(
        IOrderRepository repository,
        ILogger<CancelOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling order {BookingReference}", command.BookingReference);

        var order = await _repository.GetByBookingReferenceAsync(command.BookingReference, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {BookingReference} not found", command.BookingReference);
            return null;
        }

        if (order.OrderStatus == OrderStatusValues.Cancelled)
        {
            _logger.LogWarning("Order {BookingReference} is already cancelled", command.BookingReference);
            return null;
        }

        order.Cancel();

        // Update OrderData with cancellation info from the request body
        var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var cancelInfo = new JsonObject
        {
            ["cancelledAt"] = DateTimeOffset.UtcNow.ToString("o"),
            ["status"] = OrderStatusValues.Cancelled
        };

        // Merge request body details (reason, fees, refund amounts)
        try
        {
            var requestNode = JsonNode.Parse(command.RequestBody)?.AsObject();
            if (requestNode is not null)
            {
                foreach (var prop in requestNode)
                {
                    if (prop.Key != "version")
                        cancelInfo[prop.Key] = prop.Value?.DeepClone();
                }
            }
        }
        catch { }

        orderJson["cancellation"] = cancelInfo;

        // Add to history
        var history = orderJson["history"]?.AsArray() ?? new JsonArray();
        history.Add(new JsonObject
        {
            ["event"] = "OrderCancelled",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o")
        });
        orderJson["history"] = history;

        var updated = Domain.Entities.Order.Reconstitute(
            order.OrderId,
            order.BookingReference,
            order.OrderStatus,
            order.ChannelCode,
            order.CurrencyCode,
            order.TicketingTimeLimit,
            order.TotalAmount,
            order.Version,
            orderJson.ToJsonString(),
            order.CreatedAt,
            order.UpdatedAt);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Order {BookingReference} cancelled", command.BookingReference);

        return updated;
    }
}
