namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FareRuleResponse
{
    public Guid FareRuleId { get; init; }
    public string RuleType { get; init; } = "Money";
    public string? FlightNumber { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string BookingClass { get; init; } = string.Empty;
    public string? CurrencyCode { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public decimal? TaxAmount { get; init; }
    public int? MinPoints { get; init; }
    public int? MaxPoints { get; init; }
    public decimal? PointsTaxes { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public string? ValidFrom { get; init; }
    public string? ValidTo { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
