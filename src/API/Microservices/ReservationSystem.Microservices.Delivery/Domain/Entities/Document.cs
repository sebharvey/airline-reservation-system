namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Ancillary accountable document record — EMD (Electronic Miscellaneous Document) equivalent.
/// Maps to [delivery].[Document].
/// </summary>
public sealed class Document
{
    public Guid DocumentId { get; private set; }
    public long DocumentNumber { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string BookingReference { get; private set; } = string.Empty;
    public string? ETicketNumber { get; private set; }
    public string PassengerId { get; private set; } = string.Empty;
    public string SegmentRef { get; private set; } = string.Empty;
    public string PaymentReference { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = "GBP";
    public bool IsVoided { get; private set; }
    public string DocumentData { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Document() { }

    public static Document Create(
        string documentType,
        string bookingReference,
        string? eTicketNumber,
        string passengerId,
        string segmentRef,
        string paymentReference,
        decimal amount,
        string currencyCode,
        string documentData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        var now = DateTime.UtcNow;
        return new Document
        {
            DocumentId = Guid.NewGuid(),
            DocumentType = documentType,
            BookingReference = bookingReference,
            ETicketNumber = eTicketNumber,
            PassengerId = passengerId,
            SegmentRef = segmentRef,
            PaymentReference = paymentReference,
            Amount = amount,
            CurrencyCode = currencyCode,
            IsVoided = false,
            DocumentData = documentData,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static Document Reconstitute(
        Guid documentId, long documentNumber, string documentType,
        string bookingReference, string? eTicketNumber, string passengerId,
        string segmentRef, string paymentReference, decimal amount,
        string currencyCode, bool isVoided, string documentData,
        DateTime createdAt, DateTime updatedAt)
    {
        return new Document
        {
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            DocumentType = documentType,
            BookingReference = bookingReference,
            ETicketNumber = eTicketNumber,
            PassengerId = passengerId,
            SegmentRef = segmentRef,
            PaymentReference = paymentReference,
            Amount = amount,
            CurrencyCode = currencyCode,
            IsVoided = isVoided,
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
    public const string ProductAncillary = "ProductAncillary";
}
