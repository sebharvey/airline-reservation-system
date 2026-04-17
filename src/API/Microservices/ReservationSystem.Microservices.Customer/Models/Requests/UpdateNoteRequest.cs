namespace ReservationSystem.Microservices.Customer.Models.Requests;

public sealed class UpdateNoteRequest
{
    public string NoteText { get; init; } = string.Empty;
}
