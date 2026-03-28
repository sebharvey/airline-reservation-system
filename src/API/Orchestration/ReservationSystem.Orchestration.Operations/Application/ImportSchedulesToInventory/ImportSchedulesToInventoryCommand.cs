namespace ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;

public sealed record ImportSchedulesToInventoryCommand(
    IReadOnlyList<CabinDefinition> Cabins);

public sealed record CabinDefinition(
    string CabinCode,
    int TotalSeats,
    IReadOnlyList<FareDefinition> Fares);

public sealed record FareDefinition(
    string FareBasisCode,
    string? FareFamily,
    string? BookingClass,
    string CurrencyCode,
    decimal BaseFareAmount,
    decimal TaxAmount,
    bool IsRefundable,
    bool IsChangeable,
    decimal ChangeFeeAmount,
    decimal CancellationFeeAmount,
    int? PointsPrice,
    decimal? PointsTaxes);
