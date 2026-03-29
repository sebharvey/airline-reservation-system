namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class SearchResponse
{
    public IReadOnlyList<FlightSearchResult> Flights { get; init; } = [];
}

public sealed class FlightSearchResult
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureTime { get; init; }
    public DateTime ArrivalTime { get; init; }
    public IReadOnlyList<FareFamilyOffer> FareFamilies { get; init; } = [];
}

/// <summary>
/// Represents one fare family option on a flight.
/// Contains the OfferId for basket creation and enough price/availability info
/// for the customer to make a selection.
/// </summary>
public sealed class FareFamilyOffer
{
    public string FareFamily { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public Guid OfferId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
}
