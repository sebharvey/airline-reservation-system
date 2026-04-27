using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.AddOrderNotes;

public sealed class AddOrderNotesHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<AddOrderNotesHandler> _logger;

    public AddOrderNotesHandler(
        IOrderRepository repository,
        ILogger<AddOrderNotesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        AddOrderNotesCommand command,
        CancellationToken cancellationToken = default)
    {
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

        var existingNotesJson = orderJson["notes"]?.ToJsonString();
        orderJson.Remove("notes");
        var notesArray = existingNotesJson != null
            ? JsonNode.Parse(existingNotesJson)!.AsArray()
            : new JsonArray();

        foreach (var note in command.Notes)
        {
            var obj = new JsonObject
            {
                ["dateTime"] = note.DateTime,
                ["type"]     = note.Type,
                ["message"]  = note.Message
            };
            if (note.PaxId.HasValue) obj["paxId"] = note.PaxId.Value;
            notesArray.Add(obj);
        }

        orderJson["notes"] = notesArray;

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
            "Appended {Count} note(s) to order {BookingReference}",
            command.Notes.Count, command.BookingReference);

        return updated;
    }
}
