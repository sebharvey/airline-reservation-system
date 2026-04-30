namespace ReservationSystem.Microservices.Order.Application.AddOrderNotes;

public sealed record AddOrderNoteEntry(string DateTime, string Type, string Message, int? PaxId = null, int? SegmentId = null);

public sealed record AddOrderNotesCommand(
    string BookingReference,
    IReadOnlyList<AddOrderNoteEntry> Notes);
