using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderNote;

public sealed class UpdateOrderNoteHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderNoteHandler> _logger;

    public UpdateOrderNoteHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderNoteHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderNoteCommand command,
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
            throw new InvalidOperationException($"Order is not mutable. Current status: {order.OrderStatus}");
        }

        var orderJson = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var existingNotesJson = orderJson["notes"]?.ToJsonString();
        orderJson.Remove("notes");
        var notesArray = existingNotesJson != null
            ? JsonNode.Parse(existingNotesJson)!.AsArray()
            : new JsonArray();

        var found = false;
        var updatedNotes = new JsonArray();
        foreach (var item in notesArray)
        {
            if (item is JsonObject note && note["noteId"]?.GetValue<string>() == command.NoteId)
            {
                note["type"]    = command.Type;
                note["message"] = command.Message;
                found = true;
            }
            updatedNotes.Add(item?.DeepClone());
        }

        if (!found)
            throw new KeyNotFoundException($"Note '{command.NoteId}' not found on order {command.BookingReference}.");

        orderJson["notes"] = updatedNotes;

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
            "Updated note {NoteId} on order {BookingReference}",
            command.NoteId, command.BookingReference);

        return updated;
    }
}
