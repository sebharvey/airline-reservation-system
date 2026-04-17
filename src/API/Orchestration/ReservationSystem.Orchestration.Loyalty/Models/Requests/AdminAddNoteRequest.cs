namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class AdminAddNoteRequest
{
    public string NoteText { get; init; } = string.Empty;
}
