namespace ReservationSystem.Microservices.Seat.Domain.Entities;

/// <summary>
/// Core domain entity representing a seatmap configuration for an aircraft type.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Seatmap
{
    public Guid SeatmapId { get; private set; }
    public string AircraftTypeCode { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public string CabinLayout { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Seatmap() { }

    /// <summary>
    /// Factory method for creating a brand-new seatmap.
    /// </summary>
    public static Seatmap Create(
        string aircraftTypeCode,
        int version,
        string cabinLayout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aircraftTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinLayout);

        return new Seatmap
        {
            SeatmapId = Guid.NewGuid(),
            AircraftTypeCode = aircraftTypeCode,
            Version = version,
            IsActive = true,
            CabinLayout = cabinLayout,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static Seatmap Reconstitute(
        Guid seatmapId,
        string aircraftTypeCode,
        int version,
        bool isActive,
        string cabinLayout,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new Seatmap
        {
            SeatmapId = seatmapId,
            AircraftTypeCode = aircraftTypeCode,
            Version = version,
            IsActive = isActive,
            CabinLayout = cabinLayout,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
