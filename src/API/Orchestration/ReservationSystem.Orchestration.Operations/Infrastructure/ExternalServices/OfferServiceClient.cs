using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class OfferServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OfferServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
    }

    public async Task<FlightInventoryDto?> GetFlightInventoryAsync(
        string flightNumber,
        string departureDate,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/flights/{Uri.EscapeDataString(flightNumber)}/inventory?departureDate={departureDate}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FlightInventoryDto>(JsonOptions, cancellationToken);
    }

    public async Task<CreateFlightDto> CreateFlightAsync(
        string flightNumber,
        string departureDate,
        string departureTime,
        string arrivalTime,
        int arrivalDayOffset,
        string origin,
        string destination,
        string aircraftType,
        IReadOnlyList<(string CabinCode, int TotalSeats)> cabins,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            flightNumber,
            departureDate,
            departureTime,
            arrivalTime,
            arrivalDayOffset,
            origin,
            destination,
            aircraftType,
            cabins = cabins.Select(c => new { cabinCode = c.CabinCode, totalSeats = c.TotalSeats }).ToArray()
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/flights", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateFlightDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS create flight.");
    }

    public async Task<BatchCreateFlightsResultDto> BatchCreateFlightsAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/flights/batch", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BatchCreateFlightsResultDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS batch create flights.");
    }

    public async Task<CreateFareDto> CreateFareAsync(
        Guid inventoryId,
        string fareBasisCode,
        string? fareFamily,
        string? bookingClass,
        string currencyCode,
        decimal baseFareAmount,
        decimal taxAmount,
        bool isRefundable,
        bool isChangeable,
        decimal changeFeeAmount,
        decimal cancellationFeeAmount,
        int? pointsPrice,
        decimal? pointsTaxes,
        string validFrom,
        string validTo,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            fareBasisCode,
            fareFamily,
            bookingClass,
            currencyCode,
            baseFareAmount,
            taxAmount,
            isRefundable,
            isChangeable,
            changeFeeAmount,
            cancellationFeeAmount,
            pointsPrice,
            pointsTaxes,
            validFrom,
            validTo
        };

        var response = await _httpClient.PostAsJsonAsync($"/api/v1/flights/{inventoryId}/fares", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Inventory '{inventoryId}' not found.");

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateFareDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Offer MS create fare.");
    }
}
