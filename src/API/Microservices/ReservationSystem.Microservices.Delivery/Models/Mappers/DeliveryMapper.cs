using System.Text.Json;
using System.Text.Json.Nodes;
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
        JsonElement? ticketDataElement = null;
        decimal totalFareAmount = 0m;
        string currency = string.Empty;
        decimal totalTaxAmount = 0m;
        decimal totalAmount = 0m;
        var taxBreakdown = new List<TaxBreakdownResponse>();

        if (!string.IsNullOrWhiteSpace(ticket.TicketData) && ticket.TicketData != "{}")
        {
            ticketDataElement = JsonSerializer.Deserialize<JsonElement>(ticket.TicketData);

            var root = JsonNode.Parse(ticket.TicketData)?.AsObject();
            var fc = root?["fareConstruction"]?.AsObject();
            if (fc is not null)
            {
                totalFareAmount = fc["baseFare"]?.GetValue<decimal>() ?? 0m;
                currency = fc["currency"]?.GetValue<string>() ?? string.Empty;
                totalTaxAmount = fc["totalTaxes"]?.GetValue<decimal>() ?? 0m;
                totalAmount = fc["totalAmount"]?.GetValue<decimal>() ?? (totalFareAmount + totalTaxAmount);

                var taxesArray = fc["taxes"]?.AsArray();
                if (taxesArray is not null)
                {
                    foreach (var taxNode in taxesArray)
                    {
                        if (taxNode is not JsonObject tax) continue;
                        var couponNumbers = tax["couponNumbers"]?.AsArray()
                            ?.Select(n => n?.GetValue<int>() ?? 0)
                            .OrderBy(n => n)
                            .ToList() ?? [];
                        taxBreakdown.Add(new TaxBreakdownResponse
                        {
                            TaxCode = tax["code"]?.GetValue<string>() ?? string.Empty,
                            Amount = tax["amount"]?.GetValue<decimal>() ?? 0m,
                            Currency = tax["currency"]?.GetValue<string>() ?? currency,
                            AppliesToCouponNumbers = couponNumbers
                        });
                    }
                }
            }
        }

        // Derive structured fare components from fareCalculationLine stored in fareConstruction JSON.
        var fareCalcLine = fc?["fareCalculationLine"]?.GetValue<string>() ?? string.Empty;
        List<FareComponentResponse>? fareComponents = null;
        if (!string.IsNullOrWhiteSpace(fareCalcLine) &&
            FareCalculation.TryParse(fareCalcLine, out var parsed, out _) && parsed is not null)
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

        return new GetTicketResponse
        {
            TicketId = ticket.TicketId,
            ETicketNumber = FormatETicketNumber(ticket.TicketNumber),
            BookingReference = ticket.BookingReference,
            PassengerId = ticket.PassengerId,
            TotalFareAmount = totalFareAmount,
            Currency = currency,
            TotalTaxAmount = totalTaxAmount,
            TotalAmount = totalAmount,
            FareCalculation = fareCalcLine,
            FareComponents = fareComponents,
            TaxBreakdown = taxBreakdown,
            IsVoided = ticket.IsVoided,
            VoidedAt = ticket.VoidedAt,
            TicketData = ticketDataElement,
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
