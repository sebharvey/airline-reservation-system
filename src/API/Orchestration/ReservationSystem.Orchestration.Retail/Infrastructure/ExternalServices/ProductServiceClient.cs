using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class ProductServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProductServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    public async Task<ProductListDto?> GetAllProductsAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync("/api/v1/products", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductListDto>(JsonOptions, ct);
    }

    public async Task<ProductGroupListDto?> GetAllProductGroupsAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync("/api/v1/product-groups", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductGroupListDto>(JsonOptions, ct);
    }
}

public sealed class ProductListDto
{
    [JsonPropertyName("products")]
    public List<ProductDto> Products { get; init; } = [];
}

public sealed class ProductDto
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }

    [JsonPropertyName("productGroupId")]
    public Guid ProductGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("isSegmentSpecific")]
    public bool IsSegmentSpecific { get; init; }

    [JsonPropertyName("ssrCode")]
    public string? SsrCode { get; init; }

    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; init; }

    [JsonPropertyName("availableChannels")]
    public string AvailableChannels { get; init; } = """["WEB","APP","NDC","KIOSK","CC","AIRPORT"]""";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("prices")]
    public List<ProductPriceDto> Prices { get; init; } = [];
}

public sealed class ProductPriceDto
{
    [JsonPropertyName("priceId")]
    public Guid PriceId { get; init; }

    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

public sealed class ProductGroupListDto
{
    [JsonPropertyName("groups")]
    public List<ProductGroupDto> Groups { get; init; } = [];
}

public sealed class ProductGroupDto
{
    [JsonPropertyName("productGroupId")]
    public Guid ProductGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
