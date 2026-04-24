using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderPayments;

/// <summary>
/// Appends payment records to a confirmed order's <c>orderData.payments</c> array.
/// Called post-sale when ancillary charges are processed outside the original confirm flow.
/// </summary>
public sealed class UpdateOrderPaymentsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderPaymentsHandler> _logger;

    public UpdateOrderPaymentsHandler(IOrderRepository repository, ILogger<UpdateOrderPaymentsHandler> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderPaymentsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating payments for order {BookingReference}", command.BookingReference);

        var order = await _repository.GetByBookingReferenceAsync(command.BookingReference, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {BookingReference} not found", command.BookingReference);
            return null;
        }

        var orderJson    = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var newPayments  = JsonNode.Parse(command.PaymentsData) as JsonArray ?? [];

        var existingPayments = orderJson["payments"] as JsonArray ?? [];
        var merged = new JsonArray();
        foreach (var p in existingPayments)
            merged.Add(p?.DeepClone());
        foreach (var p in newPayments)
            merged.Add(p?.DeepClone());

        orderJson["payments"] = merged;

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

        _logger.LogInformation("Order {BookingReference} payments updated", command.BookingReference);

        return updated;
    }
}
