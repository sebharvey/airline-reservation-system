using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class IssueTicketsRequest
{
    [JsonPropertyName("basketId")] public Guid BasketId { get; init; }
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengers")] public List<PassengerDetail> Passengers { get; init; } = [];
    [JsonPropertyName("segments")] public List<SegmentDetail> Segments { get; init; } = [];
}

public sealed class PassengerDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("dob")] public string? Dob { get; init; }
    [JsonPropertyName("passengerTypeCode")] public string? PassengerTypeCode { get; init; }
    [JsonPropertyName("frequentFlyer")] public FrequentFlyerDetail? FrequentFlyer { get; init; }

    /// <summary>
    /// Per-passenger fare construction. Required for ticket issuance; must contain a parseable
    /// fareCalculationLine and taxes that sum to totalTaxes.
    /// </summary>
    [JsonPropertyName("fareConstruction")] public FareConstructionDetail? FareConstruction { get; init; }

    [JsonPropertyName("formOfPayment")] public FormOfPaymentDetail? FormOfPayment { get; init; }
    [JsonPropertyName("commission")] public CommissionDetail? Commission { get; init; }
    [JsonPropertyName("endorsementsRestrictions")] public string? EndorsementsRestrictions { get; init; }
}

public sealed class SegmentDetail
{
    [JsonPropertyName("segmentId")] public string SegmentId { get; init; } = string.Empty;
    [JsonPropertyName("inventoryId")] public Guid InventoryId { get; init; }
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("departureTime")] public string? DepartureTime { get; init; }
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("cabinName")] public string? CabinName { get; init; }
    [JsonPropertyName("fareBasisCode")] public string FareBasisCode { get; init; } = string.Empty;
    [JsonPropertyName("operatingFlightNumber")] public string? OperatingFlightNumber { get; init; }
    [JsonPropertyName("stopoverIndicator")] public string? StopoverIndicator { get; init; }
    [JsonPropertyName("baggageAllowance")] public BaggageAllowanceDetail? BaggageAllowance { get; init; }
    [JsonPropertyName("seatAssignments")] public List<SeatAssignmentDetail>? SeatAssignments { get; init; }
    [JsonPropertyName("ssrCodes")] public List<SsrCodeDetail>? SsrCodes { get; init; }
}

public sealed class SeatAssignmentDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("positionType")] public string PositionType { get; init; } = string.Empty;
    [JsonPropertyName("deckCode")] public string DeckCode { get; init; } = string.Empty;
}

public sealed class SsrCodeDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("segmentRef")] public string SegmentRef { get; init; } = string.Empty;
}

public sealed class FrequentFlyerDetail
{
    [JsonPropertyName("carrier")] public string Carrier { get; init; } = string.Empty;
    [JsonPropertyName("number")] public string Number { get; init; } = string.Empty;
    [JsonPropertyName("tier")] public string? Tier { get; init; }
}

public sealed class FareConstructionDetail
{
    [JsonPropertyName("pricingCurrency")] public string PricingCurrency { get; init; } = string.Empty;
    [JsonPropertyName("collectingCurrency")] public string CollectingCurrency { get; init; } = string.Empty;

    /// <summary>Base fare amount in the collecting currency (authoritative ticket fare).</summary>
    [JsonPropertyName("baseFare")] public decimal BaseFare { get; init; }

    [JsonPropertyName("equivalentFarePaid")] public decimal EquivalentFarePaid { get; init; }
    [JsonPropertyName("nucAmount")] public decimal NucAmount { get; init; }
    [JsonPropertyName("roeApplied")] public decimal RoeApplied { get; init; }

    /// <summary>
    /// IATA linear fare calculation string, e.g. "LON BA NYC 500.00 BA LON 500.00 NUC1000.00 END ROE0.800000".
    /// Required. Must parse successfully and component NUC sum must equal nucAmount.
    /// </summary>
    [JsonPropertyName("fareCalculationLine")] public string FareCalculationLine { get; init; } = string.Empty;

    [JsonPropertyName("taxes")] public List<TaxDetail> Taxes { get; init; } = [];
    [JsonPropertyName("totalTaxes")] public decimal TotalTaxes { get; init; }
    [JsonPropertyName("totalAmount")] public decimal TotalAmount { get; init; }
}

public sealed class TaxDetail
{
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public sealed class FormOfPaymentDetail
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("cardType")] public string? CardType { get; init; }
    [JsonPropertyName("maskedPan")] public string? MaskedPan { get; init; }
    [JsonPropertyName("expiryMmYy")] public string? ExpiryMmYy { get; init; }
    [JsonPropertyName("approvalCode")] public string? ApprovalCode { get; init; }
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
}

public sealed class CommissionDetail
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("rate")] public decimal Rate { get; init; }
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
}

public sealed class BaggageAllowanceDetail
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("quantity")] public int Quantity { get; init; }
    [JsonPropertyName("weightKg")] public int? WeightKg { get; init; }
}
