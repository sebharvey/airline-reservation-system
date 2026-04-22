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
            orderJson["rebookDetails"] = rebookNode.DeepClone();
            orderJson["rebookedAt"] = DateTime.UtcNow.ToString("o");

            // Append a history entry recording the from → to disruption change
            var historyEntry = new JsonObject
            {
                ["event"] = "IRROPSRebook",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["reason"] = rebookNode["reason"]?.GetValue<string>() ?? "FlightCancellation"
            };

            var fromFlightNumber = rebookNode["fromFlightNumber"]?.GetValue<string>();
            var fromDepartureDate = rebookNode["fromDepartureDate"]?.GetValue<string>();
            if (fromFlightNumber is not null)
            {
                historyEntry["from"] = new JsonObject
                {
                    ["flightNumber"] = fromFlightNumber,
                    ["departureDate"] = fromDepartureDate
                };
            }

            var toFlights = rebookNode["toFlights"]?.AsArray();
            if (toFlights is { Count: > 0 })
                historyEntry["to"] = toFlights.DeepClone();

            var history = orderJson["history"]?.AsArray() ?? new JsonArray();
            history.Add(historyEntry);
            orderJson["history"] = history;

            // Append an IROPS-REBOOK note recording the cancelled and replacement segment details
            var toFlightDescriptions = toFlights is { Count: > 0 }
                ? string.Join(" / ", toFlights.Select(f =>
                    $"{f["flightNumber"]?.GetValue<string>()} on {f["departureDate"]?.GetValue<string>()}"))
                : "replacement flight";

            var noteMessage = $"Segment rebooked: {fromFlightNumber ?? "unknown"} on {fromDepartureDate ?? "unknown"} → {toFlightDescriptions}";

            var existingNotesJson = orderJson["notes"]?.ToJsonString();
            orderJson.Remove("notes");
            var notesArray = existingNotesJson != null
                ? JsonNode.Parse(existingNotesJson)!.AsArray()
                : new JsonArray();

            notesArray.Add(new JsonObject
            {
                ["dateTime"] = DateTime.UtcNow.ToString("o"),
                ["type"] = "IROPS-REBOOK",
                ["message"] = noteMessage
            });

            orderJson["notes"] = notesArray;
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
            DateTime.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Order {BookingReference} rebooked, version={Version}",
            command.BookingReference, updated.Version);

        return updated;
    }
}
