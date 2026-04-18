using System.Text.Json;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.ValueObjects;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Models.Mappers;

/// <summary>
/// Static mapping methods between domain entities and HTTP responses.
/// </summary>
public static class DeliveryMapper
{
    /// <summary>
    /// IATA airline accounting code prefix prepended to the numeric <see cref="Ticket.TicketNumber"/>
    /// to form the full e-ticket number (e.g. <c>932-1000000001</c>).
    /// </summary>
    private const string AirlinePrefix = "932-";

    /// <summary>Formats a raw ticket number as the full IATA e-ticket string.</summary>
    public static string FormatETicketNumber(long ticketNumber) => $"{AirlinePrefix}{ticketNumber:D10}";

    public static TicketSummary ToTicketSummary(Ticket ticket, List<string> segmentIds) =>
        new()
        {
            TicketId = ticket.TicketId,
            ETicketNumber = FormatETicketNumber(ticket.TicketNumber),
            PassengerId = ticket.PassengerId,
            SegmentIds = segmentIds
        };

    public static GetTicketResponse ToGetTicketResponse(Ticket ticket)
    {
        JsonElement? ticketData = null;
        if (!string.IsNullOrWhiteSpace(ticket.TicketData) && ticket.TicketData != "{}")
        {
            ticketData = JsonSerializer.Deserialize<JsonElement>(ticket.TicketData);
        }

        // Derive structured fare components from the fare calculation string.
        List<FareComponentResponse>? fareComponents = null;
        if (!string.IsNullOrWhiteSpace(ticket.FareCalculation) &&
            FareCalculation.TryParse(ticket.FareCalculation, out var parsed, out _) && parsed is not null)
        {
            fareComponents = parsed.Components.Select(c => new FareComponentResponse
            {
                Origin = c.Origin,
                Carrier = c.Carrier,
                Destination = c.Destination,
                NucAmount = c.NucAmount,
                FareBasis = c.FareBasis
            }).ToList();
        }

        var taxBreakdown = ticket.TicketTaxes.Select(tx => new TaxBreakdownResponse
        {
            TaxCode = tx.TaxCode,
            Amount = tx.Amount,
            Currency = tx.Currency,
            AppliesToCouponNumbers = tx.AppliedToCoupons.Select(tc => tc.CouponNumber).OrderBy(n => n).ToList()
        }).ToList();

        return new GetTicketResponse
        {
            TicketId = ticket.TicketId,
            ETicketNumber = FormatETicketNumber(ticket.TicketNumber),
            BookingReference = ticket.BookingReference,
            PassengerId = ticket.PassengerId,
            TotalFareAmount = ticket.TotalFareAmount,
            Currency = ticket.Currency,
            TotalTaxAmount = ticket.TotalTaxAmount,
            TotalAmount = ticket.TotalAmount,
            FareCalculation = ticket.FareCalculation,
            FareComponents = fareComponents,
            TaxBreakdown = taxBreakdown,
            IsVoided = ticket.IsVoided,
            VoidedAt = ticket.VoidedAt,
            TicketData = ticketData,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Version = ticket.Version
        };
    }

    public static GetCouponValueResponse ToCouponValueResponse(string eTicketNumber, CouponValue value) =>
        new()
        {
            ETicketNumber = eTicketNumber,
            CouponNumber = value.CouponNumber,
            FareShare = value.FareShare,
            TaxShare = value.TaxShare,
            Total = value.Total,
            Currency = value.Currency
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
}
