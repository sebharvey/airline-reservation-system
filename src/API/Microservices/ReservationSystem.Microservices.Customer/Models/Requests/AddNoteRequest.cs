namespace ReservationSystem.Microservices.Customer.Models.Requests;

public sealed class AddNoteRequest
{
    public string NoteText { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
}
