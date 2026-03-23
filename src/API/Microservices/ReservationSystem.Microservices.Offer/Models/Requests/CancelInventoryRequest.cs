namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class CancelInventoryRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
}
