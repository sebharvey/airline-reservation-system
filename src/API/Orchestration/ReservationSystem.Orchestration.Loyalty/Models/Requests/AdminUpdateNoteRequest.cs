namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class AdminUpdateNoteRequest
{
    public string NoteText { get; init; } = string.Empty;
}
