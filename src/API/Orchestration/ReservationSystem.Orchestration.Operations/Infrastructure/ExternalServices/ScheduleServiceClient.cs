using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

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
    public async Task<GetSchedulesDto> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/schedules", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetSchedulesDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS get schedules.");
    }

    /// <summary>
    /// Posts a full season schedule payload to the Schedule MS POST /v1/schedules endpoint.
    /// The existing schedule table is replaced atomically by the Schedule MS.
    /// </summary>
    public async Task<ImportSsimResponse> ImportSchedulesAsync(
        object payload,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/schedules", payload, JsonOptions, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportSchedulesDto>(JsonOptions, cancellationToken);
        if (result is null)
            throw new InvalidOperationException("Empty response from Schedule MS import schedules.");

        // Map DTO to response model.
        return new ImportSsimResponse
        {
            Imported = result.Imported,
            Deleted = result.Deleted,
            Schedules = result.Schedules.Select(s => new ImportedScheduleItem
            {
                ScheduleId = s.ScheduleId,
                FlightNumber = s.FlightNumber,
                Origin = s.Origin,
                Destination = s.Destination,
                ValidFrom = s.ValidFrom,
                ValidTo = s.ValidTo,
                OperatingDateCount = s.OperatingDateCount
            }).ToList().AsReadOnly()
        };
    }
}
