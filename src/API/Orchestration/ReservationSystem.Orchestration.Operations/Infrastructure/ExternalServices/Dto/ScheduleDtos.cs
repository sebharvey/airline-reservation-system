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
