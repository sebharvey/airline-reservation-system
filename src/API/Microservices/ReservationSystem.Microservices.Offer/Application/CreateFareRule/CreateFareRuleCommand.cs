namespace ReservationSystem.Microservices.Offer.Application.CreateFareRule;

public sealed record CreateFareRuleCommand(
    string RuleType,
    string? FlightNumber,
    string FareBasisCode,
    string? FareFamily,
    string CabinCode,
    string BookingClass,
    string? CurrencyCode,
    decimal? MinAmount,
    decimal? MaxAmount,
    decimal? TaxAmount,
    int? MinPoints,
    int? MaxPoints,
    decimal? PointsTaxes,
    bool IsRefundable,
    bool IsChangeable,
    decimal ChangeFeeAmount,
    decimal CancellationFeeAmount,
    string? ValidFrom,
    string? ValidTo);
