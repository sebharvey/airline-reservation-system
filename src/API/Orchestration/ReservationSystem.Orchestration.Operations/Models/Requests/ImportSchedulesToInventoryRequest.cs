using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

/// <summary>
/// Request body for POST /v1/schedules/import-inventory.
/// Specifies the cabin and fare definitions to apply when generating inventory
/// for all schedules currently stored in the Schedule MS.
/// </summary>
public sealed class ImportSchedulesToInventoryRequest
{
    [JsonPropertyName("cabins")]
    public IReadOnlyList<CabinDefinitionRequest> Cabins { get; init; } = [];
}

public sealed class CabinDefinitionRequest
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("fares")]
    public IReadOnlyList<FareDefinitionRequest> Fares { get; init; } = [];
}

public sealed class FareDefinitionRequest
{
    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("bookingClass")]
    public string? BookingClass { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; init; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; init; }

    [JsonPropertyName("changeFeeAmount")]
    public decimal ChangeFeeAmount { get; init; }

    [JsonPropertyName("cancellationFeeAmount")]
    public decimal CancellationFeeAmount { get; init; }

    [JsonPropertyName("pointsPrice")]
    public int? PointsPrice { get; init; }

    [JsonPropertyName("pointsTaxes")]
    public decimal? PointsTaxes { get; init; }
}
