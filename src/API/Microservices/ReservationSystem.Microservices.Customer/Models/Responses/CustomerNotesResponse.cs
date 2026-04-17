namespace ReservationSystem.Microservices.Customer.Models.Responses;

public sealed class CustomerNotesResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public IReadOnlyList<CustomerNoteItem> Notes { get; init; } = [];
}

public sealed class CustomerNoteItem
{
    public Guid NoteId { get; init; }
    public string NoteText { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
