using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderPassengers;

/// <summary>
/// Handles the <see cref="UpdateOrderPassengersCommand"/>.
/// Merges passenger contact and personal detail updates into the order's
/// dataLists.passengers array, matching by passengerId.
/// </summary>
public sealed class UpdateOrderPassengersHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderPassengersHandler> _logger;

    public UpdateOrderPassengersHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderPassengersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderPassengersCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating passengers for order {BookingReference}", command.BookingReference);

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
        var updateDoc = JsonNode.Parse(command.PassengersData)?.AsObject();
        var updatesArray = updateDoc?["passengers"]?.AsArray();

        if (updatesArray is null || updatesArray.Count == 0)
            return order;

        var passengersNode = orderJson["dataLists"]?["passengers"]?.AsArray();
        if (passengersNode is null)
            return order;

        foreach (var updateItem in updatesArray)
        {
            var update = updateItem?.AsObject();
            if (update is null) continue;

            var passengerId = update["passengerId"]?.GetValue<string>();
            if (passengerId is null) continue;

            for (var i = 0; i < passengersNode.Count; i++)
            {
                var pax = passengersNode[i]?.AsObject();
                if (pax is null) continue;
                if (pax["passengerId"]?.GetValue<string>() != passengerId) continue;

                foreach (var field in new[] { "givenName", "surname", "dateOfBirth" })
                {
                    if (update[field] is JsonNode val)
                        pax[field] = val.DeepClone();
                }

                if (update["contacts"] is JsonNode contacts)
                    pax["contacts"] = contacts.DeepClone();

                if (update["travelDocument"] is JsonNode doc)
                    pax["travelDocument"] = doc.DeepClone();

                break;
            }
        }

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

        _logger.LogInformation("Order {BookingReference} passengers updated", command.BookingReference);

        return updated;
    }
}
