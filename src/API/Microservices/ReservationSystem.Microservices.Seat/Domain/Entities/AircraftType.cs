namespace ReservationSystem.Microservices.Seat.Domain.Entities;

/// <summary>
/// Core domain entity representing an aircraft type.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class AircraftType
{
    public string AircraftTypeCode { get; private set; } = string.Empty;
    public string Manufacturer { get; private set; } = string.Empty;
    public string? FriendlyName { get; private set; }
    public int TotalSeats { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AircraftType() { }

    /// <summary>
    /// Factory method for creating a brand-new aircraft type.
    /// </summary>
    public static AircraftType Create(
        string aircraftTypeCode,
        string manufacturer,
        int totalSeats,
        string? friendlyName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aircraftTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(manufacturer);

        return new AircraftType
        {
            AircraftTypeCode = aircraftTypeCode,
            Manufacturer = manufacturer,
            FriendlyName = friendlyName,
            TotalSeats = totalSeats,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static AircraftType Reconstitute(
        string aircraftTypeCode,
        string manufacturer,
        string? friendlyName,
        int totalSeats,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new AircraftType
        {
            AircraftTypeCode = aircraftTypeCode,
            Manufacturer = manufacturer,
            FriendlyName = friendlyName,
            TotalSeats = totalSeats,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
