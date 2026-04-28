using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Infrastructure.ExternalServices;

internal sealed class FlightUpdateClient : IFlightUpdateClient
{
    private readonly HttpClient _adminApiClient;
    private readonly HttpClient _retailApiClient;
    private readonly HttpClient _operationsApiClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public FlightUpdateClient(IHttpClientFactory httpClientFactory)
    {
        _adminApiClient      = httpClientFactory.CreateClient("AdminApi");
        _retailApiClient     = httpClientFactory.CreateClient("RetailApi");
        _operationsApiClient = httpClientFactory.CreateClient("OperationsApi");
    }

    public async Task<string> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var loginRequest = new AdminLoginRequest(username, password);
        var response     = await _adminApiClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AdminLoginResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Admin API login.");

        return result.AccessToken;
    }

    public async Task<List<FlightInventoryItem>> GetInventoryAsync(string departureDate, string jwtToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/inventory?departureDate={departureDate}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

        var response = await _retailApiClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<FlightInventoryItem>>(JsonOptions, ct);
        return result ?? [];
    }

    public async Task SetOperationalDataAsync(
        Guid inventoryId,
        string? departureGate,
        string? aircraftRegistration,
        string jwtToken,
        CancellationToken ct = default)
    {
        var body    = new SetFlightOperationalDataRequest(departureGate, aircraftRegistration);
        var json    = JsonSerializer.Serialize(body, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/admin/inventory/{inventoryId}/operational-data")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

        var response = await _operationsApiClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
