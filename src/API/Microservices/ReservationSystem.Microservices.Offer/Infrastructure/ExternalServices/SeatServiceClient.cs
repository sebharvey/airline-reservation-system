using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Seat microservice.
/// Used by the rolling inventory import timer trigger to resolve cabin configurations.
/// </summary>
public sealed class SeatServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SeatServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("SeatMs");
    }

    /// <summary>
    /// Retrieves all aircraft types from the Seat MS GET /v1/aircraft-types endpoint.
    /// </summary>
    public async Task<GetAircraftTypesResult> GetAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/aircraft-types", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetAircraftTypesResult>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Seat MS get aircraft types.");
    }
}

public sealed class GetAircraftTypesResult
{
    public IReadOnlyList<AircraftTypeResult> AircraftTypes { get; init; } = [];
}

public sealed class AircraftTypeResult
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public IReadOnlyList<CabinCountResult>? CabinCounts { get; init; }
}

public sealed class CabinCountResult
{
    public string Cabin { get; init; } = string.Empty;
    public int Count { get; init; }
}
