using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Schedule microservice.
/// Used by the rolling inventory import timer trigger.
/// Note: direct MS-to-MS calls are an accepted exception for timer triggers.
/// </summary>
public sealed class ScheduleServiceClient
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
    public async Task<GetSchedulesResult> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/schedules", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetSchedulesResult>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS get schedules.");
    }
}

public sealed class GetSchedulesResult
{
    public int Count { get; init; }
    public IReadOnlyList<ScheduleItemResult> Schedules { get; init; } = [];
}

public sealed class ScheduleItemResult
{
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
}
