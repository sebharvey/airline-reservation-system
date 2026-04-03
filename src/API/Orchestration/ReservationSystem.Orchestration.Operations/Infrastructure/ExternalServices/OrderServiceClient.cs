using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class OrderServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OrderServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OrderMs");
    }

    /// <summary>
    /// Retrieve an order by booking reference and surname via Order MS POST /v1/orders/retrieve.
    /// Returns null if not found.
    /// </summary>
    public async Task<OrderMsOrderResult?> RetrieveOrderAsync(string bookingReference, string surname, CancellationToken ct)
    {
        var payload = new { bookingReference, surname };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders/retrieve", payload, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsOrderResult>(JsonOptions, ct);
    }

    /// <summary>
    /// Get an order by booking reference via Order MS GET /v1/orders/{bookingRef}.
    /// Returns null if not found.
    /// </summary>
    public async Task<OrderMsOrderResult?> GetOrderAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsOrderResult>(JsonOptions, ct);
    }

    /// <summary>
    /// Update passenger travel documents on an order via Order MS PATCH /v1/orders/{bookingRef}/passengers.
    /// </summary>
    public async Task UpdateOrderPassengersAsync(string bookingReference, object passengersPayload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(passengersPayload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/passengers", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order passengers: {error}");
        }
    }
}

public sealed class OrderMsOrderResult
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("orderData")]
    public JsonElement? OrderData { get; init; }
}
