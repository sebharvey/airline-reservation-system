using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class ProductServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProductServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    // ── ProductGroup ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductGroupDto>> GetAllProductGroupsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/product-groups", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductGroupListDto>(JsonOptions, ct);
        return result?.Groups ?? [];
    }

    public async Task<ProductGroupDto?> GetProductGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/product-groups/{groupId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductGroupDto>(JsonOptions, ct);
    }

    public async Task<ProductGroupDto> CreateProductGroupAsync(object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/product-groups", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductGroupDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS create product group.");
    }

    public async Task<ProductGroupDto> UpdateProductGroupAsync(Guid groupId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/product-groups/{groupId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Product group '{groupId}' not found.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductGroupDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS update product group.");
    }

    public async Task<bool> DeleteProductGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/product-groups/{groupId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    // ── Product ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductDto>> GetAllProductsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/products", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductListDto>(JsonOptions, ct);
        return result?.Products ?? [];
    }

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/products/{productId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions, ct);
    }

    public async Task<ProductDto> CreateProductAsync(object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/products", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS create product.");
    }

    public async Task<ProductDto> UpdateProductAsync(Guid productId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/products/{productId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Product '{productId}' not found.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS update product.");
    }

    public async Task<bool> DeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/products/{productId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    // ── ProductPrice ──────────────────────────────────────────────────────────

    public async Task<ProductPriceDto> CreateProductPriceAsync(Guid productId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/products/{productId}/prices", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductPriceDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS create product price.");
    }

    public async Task<ProductPriceDto> UpdateProductPriceAsync(Guid productId, Guid priceId, object request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/products/{productId}/prices/{priceId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Product price '{priceId}' not found.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(ct));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductPriceDto>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from Ancillary MS update product price.");
    }

    public async Task<bool> DeleteProductPriceAsync(Guid productId, Guid priceId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/products/{productId}/prices/{priceId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
