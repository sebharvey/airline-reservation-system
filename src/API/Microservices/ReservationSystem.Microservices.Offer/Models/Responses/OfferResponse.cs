namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class OfferItem
{
    public Guid OfferId { get; init; }
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string? BookingClass { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
    public string BookingType { get; init; } = string.Empty;
    public int SeatsAvailable { get; init; }
    public string? OperatingCarrier { get; init; }
    public string? OperatingFlightNumber { get; init; }
    public string ExpiresAt { get; init; } = string.Empty;
}

public sealed class SearchOffersResponse
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public IReadOnlyList<OfferItem> Offers { get; init; } = [];
}

public sealed class StoredOfferResponse
{
    public Guid OfferId { get; init; }
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string? BookingClass { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
    public string BookingType { get; init; } = string.Empty;
    public int SeatsAvailable { get; init; }
    public string? OperatingCarrier { get; init; }
    public string? OperatingFlightNumber { get; init; }
    public bool IsConsumed { get; init; }
    public string ExpiresAt { get; init; } = string.Empty;
}
