namespace ReservationSystem.Microservices.Delivery.Application.GetDocument;

/// <summary>
/// Query to retrieve a document by its unique identifier.
/// </summary>
public sealed record GetDocumentQuery(Guid DocumentId);
