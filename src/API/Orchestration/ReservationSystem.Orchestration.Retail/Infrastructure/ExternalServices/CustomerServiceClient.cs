using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class CustomerServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CustomerServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CustomerMs");
    }

    /// <summary>
    /// Reinstates points to a loyalty account (e.g. on cancellation or flight change surplus).
    /// POST /api/v1/customers/{loyaltyNumber}/points/reinstate
    /// </summary>
    public async Task<int> ReinstatePointsAsync(
        string loyaltyNumber, int pointsAmount, string reason,
        CancellationToken ct)
    {
        var payload = new { pointsAmount, reason };
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/customers/{Uri.EscapeDataString(loyaltyNumber)}/points/reinstate",
            payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Points reinstatement failed: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<ReinstatePointsResult>(JsonOptions, ct);
        return result?.NewPointsBalance ?? 0;
    }
}

file sealed class ReinstatePointsResult
{
    [JsonPropertyName("newPointsBalance")]
    public int NewPointsBalance { get; init; }
}
