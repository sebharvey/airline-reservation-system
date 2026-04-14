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
        string currency, string bookingType,
        string? loyaltyNumber, int? totalPointsAmount,
        CancellationToken ct)
    {
        var payload = new { currency, bookingType, loyaltyNumber, totalPointsAmount };
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

    public async Task UpdateSsrsAsync(Guid basketId, string ssrsJson, CancellationToken ct)
    {
        using var content = new StringContent(ssrsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/api/v1/basket/{basketId}/ssrs", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update basket SSRs: {error}");
        }
    }

    public async Task UpdateProductsAsync(Guid basketId, string productsJson, CancellationToken ct)
    {
        using var content = new StringContent(productsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/api/v1/basket/{basketId}/products", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update basket products: {error}");
        }
    }

    public async Task<OrderMsCreateOrderResult> CreateOrderAsync(
        Guid basketId, string channelCode, string bookingType, string? redemptionReference,
        CancellationToken ct)
    {
        var payload = new { basketId, channelCode, bookingType, redemptionReference };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to create order: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OrderMsCreateOrderResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response creating order.");
    }

    public async Task DeleteDraftOrderAsync(Guid orderId, CancellationToken ct)
    {
        using var response = await _httpClient.DeleteAsync($"/api/v1/orders/{orderId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return; // Already gone — treat as success
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to delete draft order: {error}");
        }
    }

    public async Task<OrderMsConfirmOrderResult> ConfirmOrderAsync(
        Guid orderId, Guid basketId, List<object> paymentReferences,
        CancellationToken ct,
        object? enrichedOffers = null)
    {
        var payload = new { orderId, basketId, paymentReferences, enrichedOffers };
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

    public async Task<OrderMsOrderResult?> OciRetrieveOrderAsync(string bookingReference, string surname, CancellationToken ct)
    {
        var payload = new { bookingReference, surname };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders/oci/retrieve", payload, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsOrderResult>(JsonOptions, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetBookingReferencesAsync(
        IReadOnlyList<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return new Dictionary<Guid, string?>();
        var payload = new { orderIds };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/orders/booking-references", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode) return new Dictionary<Guid, string?>();
        var items = await response.Content.ReadFromJsonAsync<List<OrderRefItem>>(JsonOptions, ct) ?? [];
        return items.ToDictionary(x => x.OrderId, x => x.BookingReference);
    }

    public async Task<List<OrderMsOrderResult>> GetRecentOrdersAsync(int limit, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/admin/orders?limit={limit}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<OrderMsOrderResult>>(JsonOptions, ct)
            ?? new List<OrderMsOrderResult>();
    }

    public async Task UpdateOrderBagsAsync(string bookingReference, string bagsJson, CancellationToken ct)
    {
        using var content = new StringContent(bagsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/bags", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order bags: {error}");
        }
    }

    public async Task UpdateOrderPassengersAsync(string bookingReference, string passengersJson, CancellationToken ct)
    {
        using var content = new StringContent(passengersJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/passengers", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order passengers: {error}");
        }
    }

    public async Task UpdateOrderSsrsAsync(string bookingReference, string ssrsJson, CancellationToken ct)
    {
        using var content = new StringContent(ssrsJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/ssrs", content, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException(error);
        }
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order SSRs: {error}");
        }
    }

    public async Task CancelOrderAsync(string bookingReference, object requestBody, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/cancel", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to cancel order: {error}");
        }
    }

    public async Task ChangeOrderAsync(string bookingReference, object changeData, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(changeData, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/change", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to change order: {error}");
        }
    }

    public async Task UpdateOrderBagsPostSaleAsync(string bookingReference, object bagsData, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(bagsData, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/bags", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order bags: {error}");
        }
    }

    public async Task UpdateOrderSeatsPostSaleAsync(string bookingReference, object seatsData, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(seatsData, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{bookingReference}/seats", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order seats: {error}");
        }
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

    // TODO: Remove — temporary debug method
    public async Task<string?> GetOrderDebugRawAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/debug/orders/{bookingReference}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<OrderMsSsrOptionsResult> GetSsrOptionsAsync(string? cabinCode, string? flightNumbers, CancellationToken ct)
    {
        var url = "/api/v1/ssr/options";
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(cabinCode)) qs.Add($"cabinCode={Uri.EscapeDataString(cabinCode)}");
        if (!string.IsNullOrEmpty(flightNumbers)) qs.Add($"flightNumbers={Uri.EscapeDataString(flightNumbers)}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderMsSsrOptionsResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response retrieving SSR options.");
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

    [JsonPropertyName("currency")]
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed class OrderMsBasketResult
{
    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("currency")]
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

    [JsonPropertyName("currency")]
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

    [JsonPropertyName("currency")]
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

    [JsonPropertyName("currency")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("orderItems")]
    public List<ConfirmedOrderItemResult> OrderItems { get; init; } = [];
}

public sealed class ConfirmedOrderItemResult
{
    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("fareBasisCode")]
    public string? FareBasisCode { get; init; }

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("taxLines")]
    public List<ConfirmedTaxLineResult>? TaxLines { get; init; }
}

public sealed class ConfirmedTaxLineResult
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class OrderMsSsrOptionsResult
{
    [JsonPropertyName("ssrOptions")]
    public List<OrderMsSsrOptionDto> SsrOptions { get; init; } = new();
}

public sealed class OrderMsSsrOptionDto
{
    [JsonPropertyName("ssrCode")]
    public string SsrCode { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
}

public sealed class OrderRefItem
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string? BookingReference { get; init; }
}
