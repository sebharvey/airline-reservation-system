namespace ReservationSystem.Microservices.Schedule.Domain.Entities;

/// <summary>
/// Domain entity representing a named group of flight schedules (e.g. "Summer 2026").
/// Each FlightSchedule belongs to exactly one ScheduleGroup.
/// </summary>
public sealed class ScheduleGroup
{
    public Guid ScheduleGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime SeasonStart { get; private set; }
    public DateTime SeasonEnd { get; private set; }
    public bool IsActive { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ScheduleGroup() { }

    public static ScheduleGroup Create(
        string name,
        DateTime seasonStart,
        DateTime seasonEnd,
        bool isActive,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var now = DateTime.UtcNow;
        return new ScheduleGroup
        {
            ScheduleGroupId = Guid.NewGuid(),
            Name = name,
            SeasonStart = seasonStart,
            SeasonEnd = seasonEnd,
            IsActive = isActive,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string name, DateTime seasonStart, DateTime seasonEnd, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        SeasonStart = seasonStart;
        SeasonEnd = seasonEnd;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
