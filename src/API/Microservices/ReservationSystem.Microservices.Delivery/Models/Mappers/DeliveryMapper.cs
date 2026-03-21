using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Application.IssueTicket;
using ReservationSystem.Microservices.Delivery.Application.ReissueTicket;
using ReservationSystem.Microservices.Delivery.Application.UpdateManifest;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of Delivery domain objects.
///
/// Mapping directions:
///   HTTP request  →  Application command/query
///   Domain entity →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class DeliveryMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateManifestCommand ToCommand(CreateManifestRequest request) =>
        new(
            BookingReference: request.BookingReference,
            OrderId: request.OrderId,
            ManifestData: request.ManifestData);

    public static UpdateManifestCommand ToCommand(Guid manifestId, UpdateManifestRequest request) =>
        new(ManifestId: manifestId, ManifestData: request.ManifestData);

    public static IssueTicketCommand ToCommand(Guid manifestId, IssueTicketRequest request) =>
        new(
            ManifestId: manifestId,
            PassengerId: request.PassengerId,
            SegmentId: request.SegmentId,
            ETicketNumber: request.ETicketNumber);

    public static ReissueTicketCommand ToCommand(Guid manifestId, Guid ticketId, ReissueTicketRequest request) =>
        new(
            ManifestId: manifestId,
            TicketId: ticketId,
            NewETicketNumber: request.NewETicketNumber);

    public static CreateDocumentCommand ToCommand(CreateDocumentRequest request) =>
        new(
            BookingReference: request.BookingReference,
            OrderId: request.OrderId,
            DocumentType: request.DocumentType,
            DocumentData: request.DocumentData);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static ManifestResponse ToResponse(Manifest manifest) =>
        new()
        {
            ManifestId = manifest.ManifestId,
            BookingReference = manifest.BookingReference,
            OrderId = manifest.OrderId,
            ManifestStatus = manifest.ManifestStatus,
            ManifestData = manifest.ManifestData,
            CreatedAt = manifest.CreatedAt,
            UpdatedAt = manifest.UpdatedAt
        };

    public static TicketResponse ToResponse(Ticket ticket) =>
        new()
        {
            TicketId = ticket.TicketId,
            ManifestId = ticket.ManifestId,
            PassengerId = ticket.PassengerId,
            SegmentId = ticket.SegmentId,
            ETicketNumber = ticket.ETicketNumber,
            TicketStatus = ticket.TicketStatus,
            IssuedAt = ticket.IssuedAt,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };

    public static IReadOnlyList<TicketResponse> ToResponse(IEnumerable<Ticket> tickets) =>
        tickets.Select(ToResponse).ToList().AsReadOnly();

    public static DocumentResponse ToResponse(Document document) =>
        new()
        {
            DocumentId = document.DocumentId,
            BookingReference = document.BookingReference,
            OrderId = document.OrderId,
            DocumentType = document.DocumentType,
            DocumentData = document.DocumentData,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };

    public static IReadOnlyList<DocumentResponse> ToResponse(IEnumerable<Document> documents) =>
        documents.Select(ToResponse).ToList().AsReadOnly();
}
