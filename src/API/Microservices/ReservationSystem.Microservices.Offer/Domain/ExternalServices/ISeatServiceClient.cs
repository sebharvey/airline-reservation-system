namespace ReservationSystem.Microservices.Offer.Domain.ExternalServices;

/// <summary>
/// Retrieves aircraft type cabin configurations from the Seat microservice.
/// </summary>
public interface ISeatServiceClient
{
    Task<AircraftTypeData> GetAircraftTypesAsync(CancellationToken cancellationToken = default);
}

public sealed class AircraftTypeData
{
    public IReadOnlyList<AircraftTypeInfo> AircraftTypes { get; init; } = [];
}

public sealed class AircraftTypeInfo
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public IReadOnlyList<CabinCount>? CabinCounts { get; init; }
}

public sealed class CabinCount
{
    public string Cabin { get; init; } = string.Empty;
    public int Count { get; init; }
}
