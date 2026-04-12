using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Order microservice SSR catalogue endpoints.
/// All calls use the "OrderMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class OrderServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCaseIgnoreNull;

    public OrderServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OrderMs");
    }

    public async Task<SsrOptionListMsResponse> GetSsrOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/ssr/options", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SsrOptionListMsResponse>(JsonOptions, cancellationToken);
        return result ?? new SsrOptionListMsResponse();
    }

    public async Task<SsrOptionDetailMsResponse> CreateSsrOptionAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/ssr/options", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(error);
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SsrOptionDetailMsResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Order MS create SSR.");
    }

    public async Task<SsrOptionDetailMsResponse?> UpdateSsrOptionAsync(
        string ssrCode,
        object body,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/ssr/options/{ssrCode}", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SsrOptionDetailMsResponse>(JsonOptions, cancellationToken);
    }

    public async Task<bool> DeactivateSsrOptionAsync(
        string ssrCode,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/ssr/options/{ssrCode}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
