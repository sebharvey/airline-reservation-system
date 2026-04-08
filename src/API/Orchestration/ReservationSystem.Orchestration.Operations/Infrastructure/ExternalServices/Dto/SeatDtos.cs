namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class GetAircraftTypesDto
{
    public IReadOnlyList<AircraftTypeDto> AircraftTypes { get; init; } = [];
}

public sealed class AircraftTypeDto
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string? FriendlyName { get; init; }
    public int TotalSeats { get; init; }
    public IReadOnlyList<CabinCountDto>? CabinCounts { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class CabinCountDto
{
    public string Cabin { get; init; } = string.Empty;
    public int Count { get; init; }
}
