using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class BagServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public BagServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    // ── Bag Policy ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BagPolicyDto>> GetAllBagPoliciesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/bag-policies", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPoliciesListDto>(JsonOptions, ct);
        return result?.Policies ?? [];
    }

    public async Task<BagPolicyDto?> GetBagPolicyAsync(Guid policyId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/bag-policies/{policyId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BagPolicyDto>(JsonOptions, ct);
    }

    public async Task<BagPolicyDto> CreateBagPolicyAsync(object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/bag-policies", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPolicyDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS create bag policy.");
    }

    public async Task<BagPolicyDto> UpdateBagPolicyAsync(Guid policyId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/bag-policies/{policyId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Bag policy '{policyId}' not found.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPolicyDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS update bag policy.");
    }

    public async Task<bool> DeleteBagPolicyAsync(Guid policyId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/bag-policies/{policyId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    // ── Bag Pricing ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BagPricingDto>> GetAllBagPricingAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/bag-pricing", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPricingListDto>(JsonOptions, ct);
        return result?.Pricing ?? [];
    }

    public async Task<BagPricingDto?> GetBagPricingAsync(Guid pricingId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/bag-pricing/{pricingId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BagPricingDto>(JsonOptions, ct);
    }

    public async Task<BagPricingDto> CreateBagPricingAsync(object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/bag-pricing", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPricingDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS create bag pricing.");
    }

    public async Task<BagPricingDto> UpdateBagPricingAsync(Guid pricingId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/bag-pricing/{pricingId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Bag pricing '{pricingId}' not found.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BagPricingDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS update bag pricing.");
    }

    public async Task<bool> DeleteBagPricingAsync(Guid pricingId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/bag-pricing/{pricingId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
