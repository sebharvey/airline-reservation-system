namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Core domain entity representing a delivery document such as a bag or seat ancillary.
/// </summary>
public sealed class Document
{
    public Guid DocumentId { get; private set; }
    public string BookingReference { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string DocumentData { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Document() { }

    public static Document Create(
        string bookingReference,
        Guid orderId,
        string documentType,
        string documentData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);

        var now = DateTimeOffset.UtcNow;
        return new Document
        {
            DocumentId = Guid.NewGuid(),
            BookingReference = bookingReference,
            OrderId = orderId,
            DocumentType = documentType,
            DocumentData = documentData,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static Document Reconstitute(
        Guid documentId,
        string bookingReference,
        Guid orderId,
        string documentType,
        string documentData,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Document
        {
            DocumentId = documentId,
            BookingReference = bookingReference,
            OrderId = orderId,
            DocumentType = documentType,
            DocumentData = documentData,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public static class DocumentTypeValues
{
    public const string BagAncillary = "BagAncillary";
    public const string SeatAncillary = "SeatAncillary";
    public const string SsrAncillary = "SsrAncillary";
}
