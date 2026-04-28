using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

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
    /// Write check-in status onto orderItems for a departure airport via Order MS PATCH /v1/orders/{bookingRef}/checkin.
    /// Optionally includes pre-formatted timatic notes to append alongside the check-in note.
    /// </summary>
    public async Task UpdateOrderCheckInAsync(
        string bookingReference,
        string departureAirport,
        string checkedInAt,
        IReadOnlyList<OrderCheckInPassenger> passengers,
        IReadOnlyList<OrderTimaticNote>? timaticNotes,
        CancellationToken ct)
    {
        object? notesPayload = timaticNotes is { Count: > 0 }
            ? timaticNotes.Select(n => (object)new { dateTime = n.DateTime, type = n.Type, message = n.Message, paxId = n.PaxId }).ToList()
            : null;

        var payload = new { departureAirport, checkedInAt, passengers, notes = notesPayload };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/checkin", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order check-in status: {error}");
        }
    }

    /// <summary>
    /// Append notes to an order via Order MS PATCH /v1/orders/{bookingRef}/notes.
    /// Used to record timatic check results when check-in is blocked.
    /// </summary>
    public async Task AddOrderNotesAsync(
        string bookingReference,
        IReadOnlyList<OrderTimaticNote> notes,
        CancellationToken ct)
    {
        var notesPayload = notes.Select(n => (object)new { dateTime = n.DateTime, type = n.Type, message = n.Message, paxId = n.PaxId }).ToList();
        var payload = new { notes = notesPayload };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/notes", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to add order notes: {error}");
        }
    }

    /// <summary>
    /// Update passenger travel documents on an order via Order MS PATCH /v1/orders/{bookingRef}/passengers.
    /// </summary>
    public async Task UpdateOrderPassengersAsync(
        string bookingReference,
        IReadOnlyList<PassengerDocUpdate> passengers,
        CancellationToken ct)
    {
        var payload = new { passengers };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/passengers", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to update order passengers: {error}");
        }
    }

    public async Task<AffectedOrdersResponse> GetAffectedOrdersByIdsAsync(
        IReadOnlyList<Guid> orderIds,
        string flightNumber,
        string departureDate,
        CancellationToken ct)
    {
        var payload = new { orderIds, flightNumber, departureDate };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/orders/irops", payload, JsonOptions, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new AffectedOrdersResponse();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AffectedOrdersResponse>(JsonOptions, ct)
            ?? new AffectedOrdersResponse();
    }

    public async Task<AffectedOrdersResponse> GetOrdersByFlightAsync(
        string flightNumber,
        string departureDate,
        string status,
        CancellationToken ct)
    {
        var url = $"/api/v1/orders/irops?flightNumber={Uri.EscapeDataString(flightNumber)}&departureDate={departureDate}&status={status}";
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new AffectedOrdersResponse();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AffectedOrdersResponse>(JsonOptions, ct)
            ?? new AffectedOrdersResponse();
    }

    public async Task RebookOrderAsync(string bookingReference, RebookOrderRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/rebook", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to rebook order {bookingReference}: {error}");
        }
    }

    public async Task CancelOrderIropsAsync(string bookingReference, CancelOrderRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/orders/{Uri.EscapeDataString(bookingReference)}/cancel", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Failed to cancel order {bookingReference}: {error}");
        }
    }
}

public sealed class PassengerDocUpdate
{
    public string PassengerId { get; init; } = string.Empty;
    public IReadOnlyList<PassengerDoc> Docs { get; init; } = [];
}

public sealed class PassengerDoc
{
    public string Type           { get; init; } = string.Empty;
    public string Number         { get; init; } = string.Empty;
    public string IssuingCountry { get; init; } = string.Empty;
    public string Nationality    { get; init; } = string.Empty;
    public string IssueDate      { get; init; } = string.Empty;
    public string ExpiryDate     { get; init; } = string.Empty;
}

public sealed class OrderCheckInPassenger
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("ticketNumber")]
    public string TicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
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

/// <summary>
/// A pre-formatted note entry to append to an order's notes array.
/// <c>DateTime</c> is ISO 8601 UTC; <c>Type</c> is the note category (e.g. "TIMATIC");
/// <c>Message</c> is the human-readable text.
/// </summary>
public sealed class OrderTimaticNote
{
    public string DateTime { get; init; } = string.Empty;
    public string Type     { get; init; } = string.Empty;
    public string Message  { get; init; } = string.Empty;
    public int?   PaxId    { get; init; }
}
