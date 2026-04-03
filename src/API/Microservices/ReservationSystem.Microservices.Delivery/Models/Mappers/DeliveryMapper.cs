using System.Text.Json;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Models.Mappers;

/// <summary>
/// Static mapping methods between domain entities and HTTP responses.
/// </summary>
public static class DeliveryMapper
{
    public static TicketSummary ToTicketSummary(Ticket ticket, List<string> segmentIds) =>
        new()
        {
            TicketId = ticket.TicketId,
            ETicketNumber = ticket.ETicketNumber,
            PassengerId = ticket.PassengerId,
            SegmentIds = segmentIds
        };

    public static CreateDocumentResponse ToCreateDocumentResponse(Document document) =>
        new()
        {
            DocumentId = document.DocumentId,
            DocumentNumber = document.DocumentNumber,
            DocumentType = document.DocumentType,
            BookingReference = document.BookingReference,
            CreatedAt = document.CreatedAt
        };

    public static GetTicketResponse ToGetTicketResponse(Ticket ticket)
    {
        JsonElement? ticketData = null;
        if (!string.IsNullOrWhiteSpace(ticket.TicketData) && ticket.TicketData != "{}")
        {
            ticketData = JsonSerializer.Deserialize<JsonElement>(ticket.TicketData);
        }

        return new GetTicketResponse
        {
            TicketId = ticket.TicketId,
            ETicketNumber = ticket.ETicketNumber,
            BookingReference = ticket.BookingReference,
            PassengerId = ticket.PassengerId,
            IsVoided = ticket.IsVoided,
            VoidedAt = ticket.VoidedAt,
            TicketData = ticketData,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Version = ticket.Version
        };
    }

    public static GetDocumentResponse ToGetDocumentResponse(Document document)
    {
        JsonElement? docData = null;
        if (!string.IsNullOrWhiteSpace(document.DocumentData) && document.DocumentData != "{}")
        {
            docData = JsonSerializer.Deserialize<JsonElement>(document.DocumentData);
        }

        return new GetDocumentResponse
        {
            DocumentId = document.DocumentId,
            DocumentNumber = document.DocumentNumber,
            DocumentType = document.DocumentType,
            BookingReference = document.BookingReference,
            ETicketNumber = document.ETicketNumber,
            PassengerId = document.PassengerId,
            SegmentRef = document.SegmentRef,
            PaymentReference = document.PaymentReference,
            Amount = document.Amount,
            CurrencyCode = document.CurrencyCode,
            IsVoided = document.IsVoided,
            DocumentData = docData,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }
}
