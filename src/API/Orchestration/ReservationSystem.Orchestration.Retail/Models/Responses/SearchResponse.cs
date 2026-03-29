namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class SearchResponse
{
    public Guid SessionId { get; init; }
    public IReadOnlyList<FlightSearchResult> Flights { get; init; } = [];
}

public sealed class FlightSearchResult
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureTime { get; init; }
    public DateTime ArrivalTime { get; init; }
    public IReadOnlyList<CabinSearchResult> Cabins { get; init; } = [];
}

public sealed class CabinSearchResult
{
    public string CabinCode { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public decimal FromPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int? FromPoints { get; init; }
    public IReadOnlyList<FareFamilyOffer> FareFamilies { get; init; } = [];
}

/// <summary>
/// A single fare family option within a cabin, containing one offer.
/// The OfferId inside offer is passed to basket creation when the customer selects this fare.
/// </summary>
public sealed class FareFamilyOffer
{
    public string FareFamily { get; init; } = string.Empty;
    public FareOffer Offer { get; init; } = new();
}

public sealed class FareOffer
{
    public Guid OfferId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal BasePrice { get; init; }
    public decimal Tax { get; init; }
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
}
