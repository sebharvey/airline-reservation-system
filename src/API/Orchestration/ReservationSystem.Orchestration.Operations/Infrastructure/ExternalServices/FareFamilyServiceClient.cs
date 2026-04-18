using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class FareFamilyServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FareFamilyServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
    }

    public async Task<IReadOnlyList<FareFamilyDto>> GetFareFamiliesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/fare-families", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<FareFamilyDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<FareFamilyDto?> GetFareFamilyAsync(Guid fareFamilyId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/fare-families/{fareFamilyId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FareFamilyDto>(JsonOptions, cancellationToken);
    }

    public async Task<FareFamilyDto> CreateFareFamilyAsync(object request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/fare-families", request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FareFamilyDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS create fare family.");
    }

    public async Task<FareFamilyDto> UpdateFareFamilyAsync(Guid fareFamilyId, object request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/fare-families/{fareFamilyId}", request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"FareFamily '{fareFamilyId}' not found.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FareFamilyDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS update fare family.");
    }

    public async Task<bool> DeleteFareFamilyAsync(Guid fareFamilyId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/fare-families/{fareFamilyId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
