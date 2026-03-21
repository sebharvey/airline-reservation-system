namespace ReservationSystem.Microservices.Schedule.Domain.Entities;

/// <summary>
/// Core domain entity representing a flight schedule.
/// Defines the recurring pattern of flights for a given route and time window.
/// </summary>
public sealed class FlightSchedule
{
    public Guid ScheduleId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }
    public int FlightsCreatedCount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FlightSchedule() { }

    /// <summary>
    /// Factory method for creating a brand-new flight schedule. Assigns a new Id and timestamps.
    /// </summary>
    public static FlightSchedule Create(
        string flightNumber,
        string origin,
        string destination,
        DateTimeOffset validFrom,
        DateTimeOffset validTo)
    {
        return new FlightSchedule
        {
            ScheduleId = Guid.NewGuid(),
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            ValidFrom = validFrom,
            ValidTo = validTo,
            FlightsCreatedCount = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting a flight schedule from a persistence store.
    /// Does not assign a new Id or reset timestamps.
    /// </summary>
    public static FlightSchedule Reconstitute(
        Guid scheduleId,
        string flightNumber,
        string origin,
        string destination,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        int flightsCreatedCount,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new FlightSchedule
        {
            ScheduleId = scheduleId,
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            ValidFrom = validFrom,
            ValidTo = validTo,
            FlightsCreatedCount = flightsCreatedCount,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
