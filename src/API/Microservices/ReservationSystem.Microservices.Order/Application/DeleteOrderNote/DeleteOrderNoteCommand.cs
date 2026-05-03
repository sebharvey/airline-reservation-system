namespace ReservationSystem.Microservices.Order.Application.DeleteOrderNote;

public sealed record DeleteOrderNoteCommand(
    string BookingReference,
    string NoteId);
