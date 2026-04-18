using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class GetTicketResponse
{
    [JsonPropertyName("ticketId")] public Guid TicketId { get; init; }
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;

    // ── Stored fare amounts ──────────────────────────────────────────────────────
    [JsonPropertyName("totalFareAmount")] public decimal TotalFareAmount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
    [JsonPropertyName("totalTaxAmount")] public decimal TotalTaxAmount { get; init; }
    [JsonPropertyName("totalAmount")] public decimal TotalAmount { get; init; }

    /// <summary>Raw IATA linear fare calculation string (stored).</summary>
    [JsonPropertyName("fareCalculation")] public string FareCalculation { get; init; } = string.Empty;

    /// <summary>Structured fare components derived from the fare calculation string (derived, not stored).</summary>
    [JsonPropertyName("fareComponents")] public List<FareComponentResponse>? FareComponents { get; init; }

    /// <summary>Tax breakdown parsed from TicketData.fareConstruction.taxes (stored in JSON, not typed columns).</summary>
    [JsonPropertyName("taxBreakdown")] public List<TaxBreakdownResponse> TaxBreakdown { get; init; } = [];

    // ── Operational data ─────────────────────────────────────────────────────────
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("voidedAt")] public DateTime? VoidedAt { get; init; }

    /// <summary>Passenger info, coupons (with attributedTaxCodes), form of payment, change history.</summary>
    [JsonPropertyName("ticketData")] public JsonElement? TicketData { get; init; }

    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
    [JsonPropertyName("version")] public int Version { get; init; }
}

/// <summary>One component in the fare construction (derived from fareCalculation string).</summary>
public sealed class FareComponentResponse
{
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("carrier")] public string Carrier { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("nucAmount")] public decimal NucAmount { get; init; }
    [JsonPropertyName("fareBasis")] public string? FareBasis { get; init; }
}

/// <summary>A tax line with coupon attribution (from TicketData.fareConstruction.taxes).</summary>
public sealed class TaxBreakdownResponse
{
    [JsonPropertyName("taxCode")] public string TaxCode { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
    [JsonPropertyName("appliesToCouponNumbers")] public List<int> AppliesToCouponNumbers { get; init; } = [];
}
