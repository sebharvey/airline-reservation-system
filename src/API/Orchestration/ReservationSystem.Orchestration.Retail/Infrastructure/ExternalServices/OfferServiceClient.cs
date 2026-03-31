using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class OfferServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCase;

    public OfferServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
    }

    public async Task<OfferDetailDto?> GetOfferAsync(Guid offerId, Guid? sessionId = null, CancellationToken cancellationToken = default)
    {
        var url = sessionId.HasValue
            ? $"/api/v1/offers/{offerId}?sessionId={sessionId.Value}"
            : $"/api/v1/offers/{offerId}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OfferDetailDto>(JsonOptions, cancellationToken);
    }

    public async Task<OfferSearchResultDto> SearchAsync(
        string origin,
        string destination,
        string departureDate,
        int paxCount,
        string bookingType,
        CancellationToken cancellationToken = default)
    {
        var body = new { origin, destination, departureDate, paxCount, bookingType };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/search", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OfferSearchResultDto>(JsonOptions, cancellationToken);
        return result ?? new OfferSearchResultDto();
    }

    public async Task<IReadOnlyList<FlightInventoryGroupDto>> GetFlightInventoryByDateAsync(
        DateOnly departureDate,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/admin/inventory?departureDate={departureDate:yyyy-MM-dd}", cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<FlightInventoryGroupDto>>(
            JsonOptions, cancellationToken);

        return result ?? [];
    }
}
