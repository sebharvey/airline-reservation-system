using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

    public async Task<CreateScheduleDto> CreateScheduleAsync(
        object scheduleRequest,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/schedules", scheduleRequest, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateScheduleDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS create schedule.");
    }

    public async Task<UpdateScheduleDto> UpdateScheduleAsync(
        Guid scheduleId,
        int flightsCreated,
        CancellationToken cancellationToken = default)
    {
        var body = new { flightsCreated };
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/schedules/{scheduleId}", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Schedule '{scheduleId}' not found.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UpdateScheduleDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS update schedule.");
    }

    public async Task<ImportSsimResponse> ImportSsimAsync(
        string ssimText,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(ssimText, Encoding.UTF8, "text/plain");
        var response = await _httpClient.PostAsync(
            $"/api/v1/schedules/ssim?createdBy={Uri.EscapeDataString(createdBy)}",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportSsimResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Schedule MS SSIM import.");
    }
}
