using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

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

    public async Task<OrderMsCreateBasketResult> CreateBasketAsync(
        string channelCode, string currencyCode, string bookingType,
        string? loyaltyNumber, int? totalPointsAmount,
        CancellationToken ct)
    {
        var payload = new { channelCode, currencyCode, bookingType, loyaltyNumber, totalPointsAmount };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/basket", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to create basket: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OrderMsCreateBasketResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response creating basket.");
    }

    public async Task<OrderMsBasketResult?> GetBasketAsync(Guid basketId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/basket/{basketId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsBasketResult>(JsonOptions, ct);
    }

    public async Task<string?> GetBasketRawAsync(Guid basketId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/basket/{basketId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<OrderMsAddOfferResult> AddOfferAsync(Guid basketId, string offerJson, CancellationToken ct)
    {
        using var content = new StringContent(offerJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"/api/v1/basket/{basketId}/offers", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to add offer to basket: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OrderMsAddOfferResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response adding offer to basket.");
    }

    public async Task UpdatePassengersAsync(Guid basketId, string passengersJson, CancellationToken ct)
    {
        using var content = new StringContent(passengersJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/api/v1/basket/{basketId}/passengers", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update basket passengers: {error}");
        }
    }

    public async Task UpdateSeatsAsync(Guid basketId, string seatsJson, CancellationToken ct)
    {
        using var content = new StringContent(seatsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/api/v1/basket/{basketId}/seats", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update basket seats: {error}");
        }
    }

    public async Task UpdateBagsAsync(Guid basketId, string bagsJson, CancellationToken ct)
    {
        using var content = new StringContent(bagsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/api/v1/basket/{basketId}/bags", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update basket bags: {error}");
        }
    }

    public async Task<OrderMsCreateOrderResult> CreateOrderAsync(
        Guid basketId, string bookingType, string? redemptionReference,
        CancellationToken ct)
    {
        var payload = new { basketId, bookingType, redemptionReference };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to create order: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OrderMsCreateOrderResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response creating order.");
    }

    public async Task<OrderMsConfirmOrderResult> ConfirmOrderAsync(
        Guid orderId, Guid basketId, List<object> paymentReferences,
        CancellationToken ct)
    {
        var payload = new { orderId, basketId, paymentReferences };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders/confirm", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to confirm order: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OrderMsConfirmOrderResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response confirming order.");
    }

    public async Task<OrderMsOrderResult?> GetOrderByRefAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/orders/{bookingReference}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsOrderResult>(JsonOptions, ct);
    }

    public async Task<OrderMsOrderResult?> RetrieveOrderAsync(string bookingReference, string surname, CancellationToken ct)
    {
        var payload = new { bookingReference, surname };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders/retrieve", payload, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsOrderResult>(JsonOptions, ct);
    }

    public async Task<List<OrderMsOrderResult>> GetRecentOrdersAsync(int limit, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/admin/orders?limit={limit}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<OrderMsOrderResult>>(JsonOptions, ct)
            ?? new List<OrderMsOrderResult>();
    }

    public async Task UpdateOrderETicketsAsync(string bookingReference, string eTicketsJson, CancellationToken ct)
    {
        using var content = new StringContent(eTicketsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/tickets", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order e-tickets: {error}");
        }
    }
}

public sealed class OrderMsCreateBasketResult
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("basketStatus")]
    public string BasketStatus { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed class OrderMsBasketResult
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("basketStatus")]
    public string BasketStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalFareAmount")]
    public decimal? TotalFareAmount { get; init; }

    [JsonPropertyName("totalSeatAmount")]
    public decimal TotalSeatAmount { get; init; }

    [JsonPropertyName("totalBagAmount")]
    public decimal TotalBagAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("basketData")]
    public JsonElement? BasketData { get; init; }
}

public sealed class OrderMsAddOfferResult
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("basketItemId")]
    public string BasketItemId { get; init; } = string.Empty;

    [JsonPropertyName("totalFareAmount")]
    public decimal TotalFareAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }
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

    [JsonPropertyName("ticketingTimeLimit")]
    public DateTime? TicketingTimeLimit { get; init; }

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

public sealed class OrderMsCreateOrderResult
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed class OrderMsConfirmOrderResult
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}
