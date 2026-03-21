namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for creating a new aircraft type.
/// </summary>
public sealed class CreateAircraftTypeRequest
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public string? FriendlyName { get; init; }
}
