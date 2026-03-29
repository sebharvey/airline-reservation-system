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
    /// Optionally filtered by schedule group.
    /// </summary>
    public async Task<GetSchedulesDto> GetSchedulesAsync(Guid? scheduleGroupId = null, CancellationToken cancellationToken = default)
    {
        var url = "/api/v1/schedules";
        if (scheduleGroupId.HasValue)
            url += $"?scheduleGroupId={scheduleGroupId.Value}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetSchedulesDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS get schedules.");
    }

    /// <summary>
    /// Posts a full season schedule payload to the Schedule MS POST /v1/schedules endpoint.
    /// The existing schedules within the target group are replaced atomically by the Schedule MS.
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

    // ── Schedule Groups ───────────────────────────────────────────────────────

    public async Task<GetScheduleGroupsDto> GetScheduleGroupsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/schedule-groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetScheduleGroupsDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS get schedule groups.");
    }

    public async Task<ScheduleGroupItemDto> CreateScheduleGroupAsync(
        object payload,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/schedule-groups", payload, JsonOptions, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ScheduleGroupItemDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS create schedule group.");
    }

    public async Task<ScheduleGroupItemDto> UpdateScheduleGroupAsync(
        Guid scheduleGroupId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/schedule-groups/{scheduleGroupId}", payload, JsonOptions, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Schedule group '{scheduleGroupId}' not found.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ScheduleGroupItemDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS update schedule group.");
    }

    public async Task DeleteScheduleGroupAsync(Guid scheduleGroupId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/schedule-groups/{scheduleGroupId}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Schedule group '{scheduleGroupId}' not found.");

        response.EnsureSuccessStatusCode();
    }
}
