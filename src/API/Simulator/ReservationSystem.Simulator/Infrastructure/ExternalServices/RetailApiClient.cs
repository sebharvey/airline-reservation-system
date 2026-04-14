using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Infrastructure.ExternalServices;

internal sealed class RetailApiClient : IRetailApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public RetailApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("RetailApi");
    }

    public async Task<SearchSliceResponse> SearchSliceAsync(SearchSliceRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/search/slice", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchSliceResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Retail API search/slice.");
    }

    public async Task<CreateBasketResponse> CreateBasketAsync(CreateBasketRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/basket", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateBasketResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Retail API create basket.");
    }

    public async Task AddPassengersAsync(string basketId, List<PassengerRequest> passengers, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/basket/{basketId}/passengers", passengers, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task GetBasketSummaryAsync(string basketId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/basket/{basketId}/summary", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<GetBasketResponse> GetBasketAsync(string basketId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/basket/{basketId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetBasketResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Retail API get basket.");
    }

    public async Task<GetSeatmapResponse> GetSeatmapAsync(string inventoryId, string aircraftType, string flightNumber, string cabinCode, CancellationToken ct = default)
    {
        var url = $"/api/v1/flights/{inventoryId}/seatmap?aircraftType={aircraftType}&flightNumber={flightNumber}&cabinCode={cabinCode}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetSeatmapResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Retail API get seatmap.");
    }

    public async Task AddSeatsAsync(string basketId, List<SeatAssignment> seats, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/basket/{basketId}/seats", seats, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddSsrsAsync(string basketId, List<SsrRequest> ssrs, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/basket/{basketId}/ssrs", ssrs, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ConfirmBasketResponse> ConfirmBasketAsync(string basketId, ConfirmBasketRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/basket/{basketId}/confirm", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ConfirmBasketResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Retail API confirm basket.");
    }
}
