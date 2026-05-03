namespace ReservationSystem.Microservices.Order.Application.UpdateOrderNote;

public sealed record UpdateOrderNoteCommand(
    string BookingReference,
    string NoteId,
    string Type,
    string Message);
