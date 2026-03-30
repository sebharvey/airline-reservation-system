using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class PaymentServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PaymentServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("PaymentMs");
    }

    /// <summary>Returns the paymentId from the Payment MS.</summary>
    public async Task<string> InitialiseAsync(
        string paymentType, string method, string currencyCode,
        decimal amount, string description,
        CancellationToken ct)
    {
        var payload = new { paymentType, method, currencyCode, amount, description };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/payment/initialise", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Payment initialise failed: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<PaymentInitialiseResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from payment initialise.");
        return result.PaymentId;
    }

    public async Task AuthoriseAsync(
        string paymentId, decimal amount,
        string? cardNumber, string? expiryDate, string? cvv, string? cardholderName,
        CancellationToken ct)
    {
        var cardDetails = cardNumber is not null
            ? new { cardNumber, expiryDate, cvv, cardholderName }
            : (object?)null;
        var payload = new { amount, cardDetails };
        using var response = await _httpClient.PostAsJsonAsync($"/api/v1/payment/{paymentId}/authorise", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new PaymentValidationException(error);
            throw new InvalidOperationException($"Payment authorisation failed: {error}");
        }
    }

    public async Task SettleAsync(string paymentId, decimal settledAmount, CancellationToken ct)
    {
        var payload = new { settledAmount };
        using var response = await _httpClient.PostAsJsonAsync($"/api/v1/payment/{paymentId}/settle", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Payment settlement failed: {error}");
        }
    }

    public async Task VoidAsync(string paymentId, string reason, CancellationToken ct)
    {
        var payload = new { reason };
        using var response = await _httpClient.PostAsJsonAsync($"/api/v1/payment/{paymentId}/void", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Payment void failed: {error}");
        }
    }
}

file sealed class PaymentInitialiseResult
{
    [JsonPropertyName("paymentId")]
    public string PaymentId { get; init; } = string.Empty;
}
