namespace ReservationSystem.Microservices.Delivery.Application.CreateDocument;

/// <summary>
/// Command to create a new delivery document (e.g. BagAncillary, SeatAncillary).
/// </summary>
public sealed record CreateDocumentCommand(
    string BookingReference,
    Guid OrderId,
    string DocumentType,
    string DocumentData);
