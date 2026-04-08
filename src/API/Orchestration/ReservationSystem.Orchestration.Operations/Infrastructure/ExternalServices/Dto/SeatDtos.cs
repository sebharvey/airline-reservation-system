namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class GetAircraftTypesDto
{
    public IReadOnlyList<AircraftTypeDto> AircraftTypes { get; init; } = [];
}

public sealed class AircraftTypeDto
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public IReadOnlyList<CabinCountDto>? CabinCounts { get; init; }
}

public sealed class CabinCountDto
{
    public string Cabin { get; init; } = string.Empty;
    public int Count { get; init; }
}
