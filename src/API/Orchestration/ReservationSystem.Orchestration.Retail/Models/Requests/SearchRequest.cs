namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class SearchRequest
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateOnly DepartureDate { get; init; }
    public DateOnly? ReturnDate { get; init; }
    public int PassengerCount { get; init; } = 1;
}
