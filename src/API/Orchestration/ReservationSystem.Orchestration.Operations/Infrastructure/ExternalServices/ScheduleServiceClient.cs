using ReservationSystem.Orchestration.Operations.Models.Responses;
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
