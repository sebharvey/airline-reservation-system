namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// An internal contact-centre note against a customer loyalty account.
/// </summary>
public sealed class CustomerNote
{
    public Guid NoteId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string NoteText { get; private set; } = string.Empty;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CustomerNote() { }

    public static CustomerNote Create(Guid customerId, string noteText, string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noteText);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new CustomerNote
        {
            NoteId = Guid.NewGuid(),
            CustomerId = customerId,
            NoteText = noteText.Trim(),
            CreatedBy = createdBy.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void UpdateText(string noteText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noteText);
        NoteText = noteText.Trim();
    }
}
