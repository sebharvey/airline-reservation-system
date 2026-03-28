using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class FareRuleServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FareRuleServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
    }

    public async Task<IReadOnlyList<FareRuleDto>> SearchFareRulesAsync(
        string? query = null, CancellationToken cancellationToken = default)
    {
        var body = new { query = query ?? string.Empty };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/fare-rules/search", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<FareRuleDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<FareRuleDto?> GetFareRuleAsync(
        Guid fareRuleId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/fare-rules/{fareRuleId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FareRuleDto>(JsonOptions, cancellationToken);
    }

    public async Task<FareRuleDto> CreateFareRuleAsync(
        object request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/fare-rules", request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FareRuleDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS create fare rule.");
    }

    public async Task<FareRuleDto> UpdateFareRuleAsync(
        Guid fareRuleId, object request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/fare-rules/{fareRuleId}", request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"FareRule '{fareRuleId}' not found.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FareRuleDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS update fare rule.");
    }

    public async Task<bool> DeleteFareRuleAsync(
        Guid fareRuleId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/fare-rules/{fareRuleId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
