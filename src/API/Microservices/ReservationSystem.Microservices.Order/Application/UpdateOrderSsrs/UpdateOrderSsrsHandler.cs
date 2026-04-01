using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;

/// <summary>
/// Handles the <see cref="UpdateOrderSsrsCommand"/>.
/// Processes add and remove actions against the ssrItems array in OrderData.
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

        // Parse the request body: { actions: [{ action, ssrCode, passengerRef, segmentRef }] }
        JsonDocument requestDoc;
        try { requestDoc = JsonDocument.Parse(command.SsrsData); }
        catch (JsonException)
        {
            throw new InvalidOperationException("Invalid JSON in SSR update request.");
        }

        using (requestDoc)
        {
            var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();

            // Load existing ssrItems array
            var ssrItems = new List<JsonObject>();
            if (orderJson["ssrItems"] is JsonArray existingItems)
            {
                foreach (var item in existingItems)
                {
                    if (item is JsonObject obj)
                        ssrItems.Add(obj.DeepClone().AsObject());
                }
            }

            // Process each action
            if (requestDoc.RootElement.TryGetProperty("actions", out var actionsEl))
            {
                foreach (var actionEl in actionsEl.EnumerateArray())
                {
                    var action = actionEl.TryGetProperty("action", out var a) ? a.GetString() : null;
                    var ssrCode = actionEl.TryGetProperty("ssrCode", out var c) ? c.GetString() : null;
                    var passengerRef = actionEl.TryGetProperty("passengerRef", out var p) ? p.GetString() : null;
                    var segmentRef = actionEl.TryGetProperty("segmentRef", out var s) ? s.GetString() : null;

                    if (action == "add" && ssrCode is not null && passengerRef is not null && segmentRef is not null)
                    {
                        var duplicate = ssrItems.Any(i =>
                            string.Equals(i["passengerRef"]?.GetValue<string>(), passengerRef, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(i["segmentRef"]?.GetValue<string>(), segmentRef, StringComparison.OrdinalIgnoreCase));

                        if (duplicate)
                            throw new InvalidOperationException(
                                $"An SSR already exists for passenger {passengerRef} on segment {segmentRef}. Remove it before adding a new one.");

                        ssrItems.Add(new JsonObject
                        {
                            ["ssrCode"] = ssrCode,
                            ["passengerRef"] = passengerRef,
                            ["segmentRef"] = segmentRef
                        });

                        _logger.LogInformation("Added SSR {SsrCode} for passenger {PassengerRef} on segment {SegmentRef}",
                            ssrCode, passengerRef, segmentRef);
                    }
                    else if (action == "remove" && ssrCode is not null && passengerRef is not null && segmentRef is not null)
                    {
                        var removed = ssrItems.RemoveAll(i =>
                            string.Equals(i["ssrCode"]?.GetValue<string>(), ssrCode, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(i["passengerRef"]?.GetValue<string>(), passengerRef, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(i["segmentRef"]?.GetValue<string>(), segmentRef, StringComparison.OrdinalIgnoreCase));

                        _logger.LogInformation("Removed {Count} SSR {SsrCode} for passenger {PassengerRef} on segment {SegmentRef}",
                            removed, ssrCode, passengerRef, segmentRef);
                    }
                }
            }

            // Write updated ssrItems back to orderData
            var updatedArray = new JsonArray();
            foreach (var item in ssrItems)
                updatedArray.Add(item.DeepClone());
            orderJson["ssrItems"] = updatedArray;

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

            _logger.LogInformation("Order {BookingReference} SSRs updated ({Count} items)",
                command.BookingReference, ssrItems.Count);

            return updated;
        }
    }
}
