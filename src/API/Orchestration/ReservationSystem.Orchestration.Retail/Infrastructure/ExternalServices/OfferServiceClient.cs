using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class OfferServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OfferServiceClient> _logger;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCase;

    public OfferServiceClient(IHttpClientFactory httpClientFactory, ILogger<OfferServiceClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
        _logger = logger;
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

    public async Task<RepriceOfferDto?> RepriceOfferAsync(Guid offerId, Guid? sessionId = null, CancellationToken cancellationToken = default)
    {
        var url = sessionId.HasValue
            ? $"/api/v1/offers/{offerId}/reprice?sessionId={sessionId.Value}"
            : $"/api/v1/offers/{offerId}/reprice";

        using var response = await _httpClient.PostAsync(url, content: null, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RepriceOfferDto>(JsonOptions, cancellationToken);
    }

    public async Task<OfferSearchResultDto> SearchAsync(
        string origin,
        string destination,
        string departureDate,
        int paxCount,
        string bookingType,
        bool includePrivateFares = false,
        CancellationToken cancellationToken = default)
    {
        var body = new { origin, destination, departureDate, paxCount, bookingType, includePrivateFares };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/search", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OfferSearchResultDto>(JsonOptions, cancellationToken);
        return result ?? new OfferSearchResultDto();
    }

    public async Task<FlightInventoryDetailDto?> GetFlightByInventoryIdAsync(
        Guid inventoryId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/flights/{inventoryId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FlightInventoryDetailDto>(JsonOptions, cancellationToken);
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

    public async Task SellInventoryAsync(
        Guid orderId,
        IReadOnlyList<(Guid InventoryId, string CabinCode)> items,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            items = items.Select(i => new { inventoryId = i.InventoryId, cabinCode = i.CabinCode }),
            orderId
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/v1/inventory/sell", payload, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to sell inventory: {error}");
        }
    }

    public async Task ReleaseInventoryAsync(
        Guid inventoryId, string cabinCode, Guid orderId, string releaseType,
        CancellationToken cancellationToken = default)
    {
        var payload = new { inventoryId, cabinCode, orderId, releaseType };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/inventory/release", payload, JsonOptions, cancellationToken);
        // Non-fatal — inventory release failure does not prevent cancellation from completing
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(cancellationToken);
            _logger.LogWarning("[OfferServiceClient] Inventory release failed for {InventoryId}: {Error}", inventoryId, error);
        }
    }

    public async Task<IReadOnlyList<FlightInventoryHoldDto>> GetInventoryHoldsAsync(
        Guid inventoryId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/inventory/{inventoryId}/holds", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<FlightInventoryHoldDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task HoldInventoryAsync(
        Guid inventoryId, string cabinCode, IReadOnlyList<(string? SeatNumber, string? PassengerId)> passengers, Guid orderId,
        string holdType = "Revenue", short? standbyPriority = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            inventoryId,
            cabinCode,
            passengers = passengers.Select(p => new { seatNumber = p.SeatNumber, passengerId = p.PassengerId }).ToList(),
            orderId,
            holdType,
            standbyPriority
        };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/inventory/hold", payload, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(cancellationToken);
            throw new InvalidOperationException($"Inventory hold failed: {error}");
        }
    }
}
