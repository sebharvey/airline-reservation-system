namespace ReservationSystem.Microservices.Customer.Application.AddNote;

public sealed record AddNoteCommand(string LoyaltyNumber, string NoteText, string CreatedBy);
