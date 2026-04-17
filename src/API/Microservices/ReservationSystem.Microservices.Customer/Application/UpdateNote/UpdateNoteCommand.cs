namespace ReservationSystem.Microservices.Customer.Application.UpdateNote;

public sealed record UpdateNoteCommand(string LoyaltyNumber, Guid NoteId, string NoteText);
