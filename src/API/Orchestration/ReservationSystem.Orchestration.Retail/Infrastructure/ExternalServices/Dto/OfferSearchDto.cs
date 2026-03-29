namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

/// <summary>
/// A single cabin fare item within a flight offer returned by the Offer MS search.
/// </summary>
public sealed class OfferItemDto
{
    public string CabinCode { get; init; } = string.Empty;
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public string BookingType { get; init; } = string.Empty;
    public int SeatsAvailable { get; init; }
}

/// <summary>
/// A single flight in the grouped search response from the Offer MS.
/// </summary>
public sealed class FlightItemDto
{
    public Guid OfferId { get; init; }
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public string ExpiresAt { get; init; } = string.Empty;
    public IReadOnlyList<OfferItemDto> Offers { get; init; } = [];
}

/// <summary>
/// Top-level search result from POST /v1/search on the Offer MS.
/// </summary>
public sealed class OfferSearchResultDto
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public IReadOnlyList<FlightItemDto> Flights { get; init; } = [];
}

/// <summary>
/// Full stored offer detail returned by GET /v1/offers/{offerId} on the Offer MS.
/// </summary>
public sealed class OfferDetailDto
{
    public Guid OfferId { get; init; }
    public string ExpiresAt { get; init; } = string.Empty;
    public OfferDetailFaresInfoDto FaresInfo { get; init; } = new();
}

public sealed class OfferDetailFaresInfoDto
{
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<OfferItemDto> Offers { get; init; } = [];
}
