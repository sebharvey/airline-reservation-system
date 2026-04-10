namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class SearchConnectingRequest
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public int PaxCount { get; init; } = 1;
    public string BookingType { get; init; } = "Revenue";
}
