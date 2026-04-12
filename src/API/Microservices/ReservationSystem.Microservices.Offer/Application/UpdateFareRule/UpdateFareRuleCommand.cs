namespace ReservationSystem.Microservices.Offer.Application.UpdateFareRule;

public sealed record UpdateFareRuleCommand(
    Guid FareRuleId,
    string RuleType,
    string? FlightNumber,
    string FareBasisCode,
    string? FareFamily,
    string CabinCode,
    string BookingClass,
    string? CurrencyCode,
    decimal? MinAmount,
    decimal? MaxAmount,
    int? MinPoints,
    int? MaxPoints,
    decimal? PointsTaxes,
    string? TaxLines,
    bool IsRefundable,
    bool IsChangeable,
    decimal ChangeFeeAmount,
    decimal CancellationFeeAmount,
    string? ValidFrom,
    string? ValidTo);
