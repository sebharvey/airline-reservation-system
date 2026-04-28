using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class SeatServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCase;

    public SeatServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    public async Task<SeatmapLayoutDto?> GetSeatmapAsync(string aircraftType, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/seatmap/{Uri.EscapeDataString(aircraftType)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SeatmapLayoutDto>(JsonOptions, cancellationToken);
    }

    public async Task<SeatOffersDto?> GetSeatOffersAsync(Guid flightId, string aircraftType, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/seat-offers?flightId={flightId}&aircraftType={Uri.EscapeDataString(aircraftType)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SeatOffersDto>(JsonOptions, cancellationToken);
    }

    public async Task<SeatOfferDto?> GetSeatOfferByIdAsync(string seatOfferId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/seat-offers/{Uri.EscapeDataString(seatOfferId)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SeatOfferDto>(JsonOptions, cancellationToken);
    }

    // ── Admin CRUD ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AircraftTypeDto>> GetAllAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/v1/aircraft-types", cancellationToken);
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<AircraftTypeListWrapper>(JsonOptions, cancellationToken);
        return wrapper?.AircraftTypes ?? Array.Empty<AircraftTypeDto>();
    }

    public async Task<IReadOnlyList<SeatPricingDto>> GetAllSeatPricingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/v1/seat-pricing", cancellationToken);
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<SeatPricingListWrapper>(JsonOptions, cancellationToken);
        return wrapper?.Pricing ?? Array.Empty<SeatPricingDto>();
    }

    public async Task<SeatPricingDto?> GetSeatPricingByIdAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/seat-pricing/{seatPricingId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SeatPricingDto>(JsonOptions, cancellationToken);
    }

    public async Task<(SeatPricingDto? Result, HttpStatusCode Status, string? ErrorBody)> CreateSeatPricingAsync(
        CreateSeatPricingRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/seat-pricing", request, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (null, response.StatusCode, body);
        }
        var created = await response.Content.ReadFromJsonAsync<SeatPricingDto>(JsonOptions, cancellationToken);
        return (created, response.StatusCode, null);
    }

    public async Task<(SeatPricingDto? Result, HttpStatusCode Status, string? ErrorBody)> UpdateSeatPricingAsync(
        Guid seatPricingId, UpdateSeatPricingRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/api/v1/seat-pricing/{seatPricingId}", request, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (null, response.StatusCode, body);
        }
        var updated = await response.Content.ReadFromJsonAsync<SeatPricingDto>(JsonOptions, cancellationToken);
        return (updated, response.StatusCode, null);
    }

    public async Task<HttpStatusCode> DeleteSeatPricingAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"/api/v1/seat-pricing/{seatPricingId}", cancellationToken);
        return response.StatusCode;
    }
}
