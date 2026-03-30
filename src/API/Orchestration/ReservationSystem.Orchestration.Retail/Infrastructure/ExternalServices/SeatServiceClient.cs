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
        _httpClient = httpClientFactory.CreateClient("SeatMs");
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
}
