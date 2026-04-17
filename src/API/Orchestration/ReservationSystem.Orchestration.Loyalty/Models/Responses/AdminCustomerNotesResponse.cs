namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class AdminCustomerNotesResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public IReadOnlyList<AdminCustomerNoteItem> Notes { get; init; } = [];
}

public sealed class AdminCustomerNoteItem
{
    public Guid NoteId { get; init; }
    public string NoteText { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
