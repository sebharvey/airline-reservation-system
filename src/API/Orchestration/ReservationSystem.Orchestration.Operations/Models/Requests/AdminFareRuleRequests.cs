namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminSearchFareRulesRequest
{
    public string? Query { get; init; }
}

public sealed class AdminCreateFareRuleRequest
{
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
}

public sealed class AdminUpdateFareRuleRequest
{
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
}
