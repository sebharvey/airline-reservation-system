using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Microservices.Offer.Domain.ExternalServices;

namespace ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Schedule microservice.
/// Used by the rolling inventory import timer trigger.
/// Note: direct MS-to-MS calls are an accepted exception for timer triggers.
/// </summary>
public sealed class ScheduleServiceClient : IScheduleServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ScheduleServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ScheduleMs");
    }

    /// <summary>
    /// Retrieves all flight schedules from the Schedule MS GET /v1/schedules endpoint.
    /// </summary>
    public async Task<ScheduleData> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/schedules", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ScheduleData>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS get schedules.");
    }
}
