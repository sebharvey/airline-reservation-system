namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SearchOffersRequest
{
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public int PaxCount { get; init; }
    public string BookingType { get; init; } = "Revenue";
}
