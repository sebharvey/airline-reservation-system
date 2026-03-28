namespace ReservationSystem.Microservices.Offer.Application.UpdateFareRule;

public sealed record UpdateFareRuleCommand(
    Guid FareRuleId,
    string? FlightNumber,
    string FareBasisCode,
    string? FareFamily,
    string CabinCode,
    string BookingClass,
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
