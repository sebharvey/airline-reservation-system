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
