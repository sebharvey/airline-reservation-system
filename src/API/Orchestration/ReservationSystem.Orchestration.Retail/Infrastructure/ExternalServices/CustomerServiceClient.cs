using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class CustomerServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CustomerServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CustomerMs");
    }

    public async Task<string?> GetLoyaltyNumberByIdentityIdAsync(
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/customers/by-identity/{identityId}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CustomerIdentityLookupResult>(JsonOptions, cancellationToken);
        return result?.LoyaltyNumber;
    }

    /// <summary>
    /// Reinstates points to a loyalty account (e.g. on cancellation or flight change surplus).
    /// POST /api/v1/customers/{loyaltyNumber}/points/reinstate
    /// </summary>
    public async Task<int> ReinstatePointsAsync(
        string loyaltyNumber, int pointsAmount, string reason,
        CancellationToken ct)
    {
        var payload = new { pointsAmount, reason };
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/customers/{Uri.EscapeDataString(loyaltyNumber)}/points/reinstate",
            payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Points reinstatement failed: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<ReinstatePointsResult>(JsonOptions, ct);
        return result?.NewPointsBalance ?? 0;
    }

    /// <summary>
    /// Links a confirmed order to a customer loyalty account.
    /// Silently no-ops when the loyalty number is null or blank.
    /// </summary>
    public async Task LinkOrderToCustomerAsync(
        string loyaltyNumber, Guid orderId, string bookingReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loyaltyNumber))
            return;

        var payload = new { orderId, bookingReference };
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/customers/{Uri.EscapeDataString(loyaltyNumber)}/orders",
            payload, JsonOptions, cancellationToken);

        // Non-2xx responses are intentionally swallowed — order linking is
        // a best-effort operation that must not roll back a confirmed booking.
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.Error.WriteLine(
                $"[CustomerServiceClient] Failed to link order {bookingReference} to {loyaltyNumber}: {response.StatusCode} — {body}");
        }
    }
}

file sealed class ReinstatePointsResult
{
    [JsonPropertyName("newPointsBalance")]
    public int NewPointsBalance { get; init; }
}

file sealed class CustomerIdentityLookupResult
{
    [JsonPropertyName("loyaltyNumber")]
    public string LoyaltyNumber { get; init; } = string.Empty;
}
