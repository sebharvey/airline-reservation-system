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

    public static ManifestEntrySummary ToManifestSummary(Manifest manifest) =>
        new()
        {
            ManifestId = manifest.ManifestId,
            BookingReference = manifest.BookingReference,
            ETicketNumber = manifest.ETicketNumber,
            PassengerId = manifest.PassengerId,
            FlightNumber = manifest.FlightNumber,
            DepartureDate = manifest.DepartureDate.ToString("yyyy-MM-dd"),
            SeatNumber = manifest.SeatNumber
        };

    public static ManifestEntryDetail ToManifestDetail(Manifest manifest) =>
        new()
        {
            ManifestId = manifest.ManifestId,
            BookingReference = manifest.BookingReference,
            ETicketNumber = manifest.ETicketNumber,
            PassengerId = manifest.PassengerId,
            GivenName = manifest.GivenName,
            Surname = manifest.Surname,
            SeatNumber = manifest.SeatNumber,
            CabinCode = manifest.CabinCode,
            SsrCodes = ParseSsrCodes(manifest.SsrCodes),
            DepartureTime = manifest.DepartureTime.ToString(@"hh\:mm"),
            ArrivalTime = manifest.ArrivalTime.ToString(@"hh\:mm"),
            CheckedIn = manifest.CheckedIn,
            CheckedInAt = manifest.CheckedInAt
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

    private static List<string> ParseSsrCodes(string? ssrCodesJson)
    {
        if (string.IsNullOrWhiteSpace(ssrCodesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(ssrCodesJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
