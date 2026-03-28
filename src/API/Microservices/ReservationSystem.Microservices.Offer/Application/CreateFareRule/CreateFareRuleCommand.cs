namespace ReservationSystem.Microservices.Offer.Application.CreateFareRule;

public sealed record CreateFareRuleCommand(
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
    string? ValidFrom,
    string? ValidTo);
