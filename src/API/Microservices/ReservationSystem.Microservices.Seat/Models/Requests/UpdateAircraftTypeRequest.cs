namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for updating an existing aircraft type.
/// </summary>
public sealed class UpdateAircraftTypeRequest
{
    public string Manufacturer { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public string? FriendlyName { get; init; }
    public bool IsActive { get; init; }
}
