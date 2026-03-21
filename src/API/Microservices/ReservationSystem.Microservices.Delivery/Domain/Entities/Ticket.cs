namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Core domain entity representing an electronic ticket issued on a manifest.
/// </summary>
public sealed class Ticket
{
    public Guid TicketId { get; private set; }
    public Guid ManifestId { get; private set; }
    public Guid PassengerId { get; private set; }
    public Guid SegmentId { get; private set; }
    public string ETicketNumber { get; private set; } = string.Empty;
    public string TicketStatus { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Ticket() { }

    public static Ticket Create(
        Guid manifestId,
        Guid passengerId,
        Guid segmentId,
        string eTicketNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eTicketNumber);

        var now = DateTimeOffset.UtcNow;
        return new Ticket
        {
            TicketId = Guid.NewGuid(),
            ManifestId = manifestId,
            PassengerId = passengerId,
            SegmentId = segmentId,
            ETicketNumber = eTicketNumber,
            TicketStatus = TicketStatusValues.Issued,
            IssuedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static Ticket Reconstitute(
        Guid ticketId,
        Guid manifestId,
        Guid passengerId,
        Guid segmentId,
        string eTicketNumber,
        string ticketStatus,
        DateTimeOffset issuedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Ticket
        {
            TicketId = ticketId,
            ManifestId = manifestId,
            PassengerId = passengerId,
            SegmentId = segmentId,
            ETicketNumber = eTicketNumber,
            TicketStatus = ticketStatus,
            IssuedAt = issuedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Reissue(string newETicketNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newETicketNumber);
        ETicketNumber = newETicketNumber;
        TicketStatus = TicketStatusValues.Reissued;
        IssuedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Void()
    {
        TicketStatus = TicketStatusValues.Voided;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public static class TicketStatusValues
{
    public const string Issued = "issued";
    public const string Reissued = "reissued";
    public const string Voided = "voided";
}
