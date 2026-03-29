namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class OfferSearchResultDto
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public IReadOnlyList<OfferItemDto> Offers { get; init; } = [];
}

public sealed class OfferItemDto
{
    public Guid OfferId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
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
    public string ExpiresAt { get; init; } = string.Empty;
}
