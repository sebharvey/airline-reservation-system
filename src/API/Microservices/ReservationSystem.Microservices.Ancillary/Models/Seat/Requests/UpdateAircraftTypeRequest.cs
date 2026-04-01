using ReservationSystem.Microservices.Ancillary.Models.Seat;

namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;

/// <summary>
/// HTTP request body for updating an existing aircraft type.
/// </summary>
public sealed class UpdateAircraftTypeRequest
{
    public string Manufacturer { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public string? FriendlyName { get; init; }
    public List<CabinCount>? CabinCounts { get; init; }
    public bool IsActive { get; init; }
}
