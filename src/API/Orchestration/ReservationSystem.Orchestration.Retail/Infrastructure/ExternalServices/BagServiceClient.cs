using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class BagServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public BagServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    public async Task<BagOfferValidationDto?> GetBagOfferAsync(string bagOfferId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/bags/offers/{Uri.EscapeDataString(bagOfferId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Bag offer validation failed for {bagOfferId}: {error}");
        }
        return await response.Content.ReadFromJsonAsync<BagOfferValidationDto>(JsonOptions, ct);
    }

    public async Task<BagOffersResponseDto?> GetBagOffersAsync(string inventoryId, string cabinCode, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/bags/offers?inventoryId={Uri.EscapeDataString(inventoryId)}&cabinCode={Uri.EscapeDataString(cabinCode)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BagOffersResponseDto>(JsonOptions, ct);
    }
}

public sealed class BagOfferValidationDto
{
    [JsonPropertyName("bagOfferId")]
    public string BagOfferId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }
}

public sealed class BagOffersResponseDto
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("policy")]
    public BagPolicyInfoDto? Policy { get; init; }

    [JsonPropertyName("bagOffers")]
    public List<BagOfferItemDto> BagOffers { get; init; } = [];
}

public sealed class BagPolicyInfoDto
{
    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }
}

public sealed class BagOfferItemDto
{
    [JsonPropertyName("bagOfferId")]
    public string BagOfferId { get; init; } = string.Empty;

    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}
