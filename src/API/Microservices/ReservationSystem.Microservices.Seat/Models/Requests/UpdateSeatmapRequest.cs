namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for updating an existing seatmap.
/// </summary>
public sealed class UpdateSeatmapRequest
{
    public string? CabinLayout { get; init; }
    public bool? IsActive { get; init; }
}
