using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Microservices.Offer.Domain.ExternalServices;

namespace ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Seat microservice.
/// Used by the rolling inventory import timer trigger to resolve cabin configurations.
/// </summary>
public sealed class SeatServiceClient : ISeatServiceClient
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
    public async Task<AircraftTypeData> GetAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/aircraft-types", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AircraftTypeData>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Seat MS get aircraft types.");
    }
}
