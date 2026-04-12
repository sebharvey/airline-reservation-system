namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

/// <summary>
/// A single cabin fare item within a flight offer. OfferId identifies this
/// specific fare so the basket flow can reference it directly.
/// </summary>
public sealed class OfferItemDto
{
    public Guid OfferId { get; init; }
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
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
}

/// <summary>
/// A single flight in the grouped search response from the Offer MS.
/// Flight details come from FlightInventory; fare offers come from FaresInfo.
/// </summary>
public sealed class FlightItemDto
{
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
    public Guid SessionId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public IReadOnlyList<FlightItemDto> Flights { get; init; } = [];
}

/// <summary>
/// Full offer detail returned by GET /v1/offers/{offerId} on the Offer MS.
/// Flight fields are resolved from FlightInventory at read time; fare items come from FaresInfo.
/// </summary>
public sealed class OfferDetailDto
{
    public Guid StoredOfferId { get; init; }
    public Guid OfferId => StoredOfferId;
    public Guid SessionId { get; init; }
    public string ExpiresAt { get; init; } = string.Empty;
    public Guid InventoryId { get; init; }
    public bool Validated { get; init; }
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
