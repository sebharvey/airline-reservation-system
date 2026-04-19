using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Payment microservice (read-only operations).
/// Uses the "PaymentMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class PaymentServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCaseIgnoreNull;

    public PaymentServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("PaymentMs");
    }

    public async Task<IReadOnlyList<PaymentListItemMsResponse>> GetPaymentsByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/payment?date={date:yyyy-MM-dd}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<PaymentListItemMsResponse>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<PaymentMsResponse?> GetPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/payment/{paymentId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentMsResponse>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentEventMsResponse>> GetPaymentEventsAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/payment/{paymentId}/events", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<PaymentEventMsResponse>>(JsonOptions, cancellationToken);
        return result ?? [];
    }
}
