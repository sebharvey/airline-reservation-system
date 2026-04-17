namespace ReservationSystem.Microservices.Customer.Application.DeleteNote;

public sealed record DeleteNoteCommand(string LoyaltyNumber, Guid NoteId);
