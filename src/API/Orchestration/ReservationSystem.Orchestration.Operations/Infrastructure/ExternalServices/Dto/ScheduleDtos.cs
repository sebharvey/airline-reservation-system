namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

/// <summary>
/// DTO for the Schedule MS POST /v1/schedules response.
/// </summary>
public sealed class ImportSchedulesDto
{
    public int Imported { get; init; }
    public int Deleted { get; init; }
    public IReadOnlyList<ImportedScheduleItemDto> Schedules { get; init; } = [];
}

public sealed class ImportedScheduleItemDto
{
    public Guid ScheduleId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
    public int OperatingDateCount { get; init; }
}

/// <summary>
/// DTO for the Schedule MS GET /v1/schedules response.
/// </summary>
public sealed class GetSchedulesDto
{
    public int Count { get; init; }
    public IReadOnlyList<ScheduleItemDto> Schedules { get; init; } = [];
}

public sealed class ScheduleItemDto
{
    public Guid ScheduleId { get; init; }
    public Guid ScheduleGroupId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public int DaysOfWeek { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
    public int FlightsCreated { get; init; }
    public int OperatingDateCount { get; init; }
}

/// <summary>
/// DTO for the Schedule MS GET /v1/schedule-groups response.
/// </summary>
public sealed class GetScheduleGroupsDto
{
    public int Count { get; init; }
    public IReadOnlyList<ScheduleGroupItemDto> Groups { get; init; } = [];
}

public sealed class ScheduleGroupItemDto
{
    public Guid ScheduleGroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SeasonStart { get; init; } = string.Empty;
    public string SeasonEnd { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int ScheduleCount { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
