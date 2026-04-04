using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;

/// <summary>
/// Handles the <see cref="UpdateOrderCheckInCommand"/>.
/// Writes a <c>checkin</c> object onto each orderItem whose <c>origin</c>
/// matches the departure airport, recording per-passenger check-in status.
/// </summary>
public sealed class UpdateOrderCheckInHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderCheckInHandler> _logger;

    public UpdateOrderCheckInHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderCheckInHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Writing check-in status to order {BookingReference} for departure {DepartureAirport}",
            command.BookingReference, command.DepartureAirport);

        var order = await _repository.GetByBookingReferenceAsync(command.BookingReference, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {BookingReference} not found", command.BookingReference);
            return null;
        }

        if (order.OrderStatus != OrderStatusValues.Confirmed && order.OrderStatus != OrderStatusValues.Changed)
        {
            _logger.LogWarning(
                "Order {BookingReference} is not mutable (status: {Status})",
                command.BookingReference, order.OrderStatus);
            throw new InvalidOperationException($"Order is not mutable. Current status: {order.OrderStatus}");
        }

        var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();

        // Build the checkin object shared across all matching orderItems
        var passengerNodes = new JsonArray();
        foreach (var pax in command.Passengers)
        {
            passengerNodes.Add(new JsonObject
            {
                ["passengerId"] = pax.PassengerId,
                ["ticketNumber"] = pax.TicketNumber,
                ["status"] = pax.Status,
                ["message"] = pax.Message
            });
        }

        var overallStatus = command.Passengers.Count > 0 &&
                            command.Passengers.All(p => p.Status == "CheckedIn")
            ? "CheckedIn"
            : "PartiallyCheckedIn";

        // Apply checkin object to each orderItem departing from the given airport
        if (orderJson["orderItems"] is JsonArray orderItems)
        {
            foreach (var item in orderItems)
            {
                if (item is not JsonObject itemObj) continue;

                var origin = itemObj["origin"]?.GetValue<string>();
                if (!string.Equals(origin, command.DepartureAirport, StringComparison.OrdinalIgnoreCase))
                    continue;

                itemObj["checkin"] = new JsonObject
                {
                    ["status"] = overallStatus,
                    ["checkedInAt"] = command.CheckedInAt,
                    ["passengers"] = passengerNodes.DeepClone()
                };
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

        _logger.LogInformation(
            "Check-in status written to order {BookingReference}", command.BookingReference);

        return updated;
    }
}
