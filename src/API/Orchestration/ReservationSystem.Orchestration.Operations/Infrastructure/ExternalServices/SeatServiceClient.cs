using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

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
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    /// <summary>
    /// Retrieves all aircraft types from the Seat MS GET /v1/aircraft-types endpoint.
    /// </summary>
    public async Task<GetAircraftTypesDto> GetAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/aircraft-types", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetAircraftTypesDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Seat MS get aircraft types.");
    }
}
