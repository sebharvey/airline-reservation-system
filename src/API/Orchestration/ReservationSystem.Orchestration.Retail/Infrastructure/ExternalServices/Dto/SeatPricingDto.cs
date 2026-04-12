namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

/// <summary>Seat pricing rule as returned by the Ancillary microservice.</summary>
public sealed record SeatPricingDto(
    Guid SeatPricingId,
    string CabinCode,
    string SeatPosition,
    string CurrencyCode,
    decimal Price,
    bool IsActive,
    DateTime ValidFrom,
    DateTime? ValidTo,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Wrapper matching the { "pricing": [...] } envelope from GET /v1/seat-pricing.</summary>
internal sealed record SeatPricingListWrapper(IReadOnlyList<SeatPricingDto> Pricing);

/// <summary>Request body forwarded to the Ancillary microservice to create a seat pricing rule.</summary>
public sealed record CreateSeatPricingRequestDto(
    string CabinCode,
    string SeatPosition,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo);

/// <summary>Request body forwarded to the Ancillary microservice to update a seat pricing rule.</summary>
public sealed record UpdateSeatPricingRequestDto(
    string? CabinCode,
    string? SeatPosition,
    string? CurrencyCode,
    decimal? Price,
    bool? IsActive,
    DateTime? ValidFrom,
    DateTime? ValidTo);
