using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Microservices.Offer.Domain.ExternalServices;

namespace ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Ancillary microservice, used by the rolling inventory import timer trigger to resolve cabin configurations.
/// TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins and should be
/// derived from existing inventory rows for the same AircraftType rather than fetched from the Ancillary MS.
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
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
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
