namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class FareRuleDto
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
    public int? MinPoints { get; init; }
    public int? MaxPoints { get; init; }
    public decimal? PointsTaxes { get; init; }
    public object[]? TaxLines { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public string? ValidFrom { get; init; }
    public string? ValidTo { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;

    /// <summary>Sums all tax line amounts. TaxLines elements are JsonElement at runtime.</summary>
    public decimal GetTotalTaxAmount()
    {
        if (TaxLines is null || TaxLines.Length == 0) return 0m;
        try
        {
            return TaxLines
                .OfType<System.Text.Json.JsonElement>()
                .Sum(e => e.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m);
        }
        catch { return 0m; }
    }
}
