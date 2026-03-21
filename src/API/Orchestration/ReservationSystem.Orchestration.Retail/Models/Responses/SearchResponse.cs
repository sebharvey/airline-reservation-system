namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class SearchResponse
{
    public IReadOnlyList<FlightOffer> Offers { get; init; } = [];
}

public sealed class FlightOffer
{
    public Guid OfferId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTimeOffset DepartureTime { get; init; }
    public DateTimeOffset ArrivalTime { get; init; }
    public string CabinClass { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
}
