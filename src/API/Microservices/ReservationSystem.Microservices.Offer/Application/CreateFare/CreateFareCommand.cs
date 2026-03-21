namespace ReservationSystem.Microservices.Offer.Application.CreateFare;

public sealed record CreateFareCommand(
    Guid InventoryId,
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
    decimal? PointsTaxes,
    string ValidFrom,
    string ValidTo);
