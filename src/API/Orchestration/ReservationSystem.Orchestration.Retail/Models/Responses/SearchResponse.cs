namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class SearchResponse
{
    public IReadOnlyList<FlightSearchResult> Flights { get; init; } = [];
}

public sealed class FlightSearchResult
{
    public Guid OfferId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureTime { get; init; }
    public DateTime ArrivalTime { get; init; }
    public IReadOnlyList<CabinOffer> Offers { get; init; } = [];
}

public sealed class CabinOffer
{
    public string CabinCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public bool IsRefundable { get; init; }
    public string? FareFamily { get; init; }
}
