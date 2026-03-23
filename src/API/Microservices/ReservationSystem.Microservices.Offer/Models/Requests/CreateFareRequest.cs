namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class CreateFareRequest
{
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string? BookingClass { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
}
