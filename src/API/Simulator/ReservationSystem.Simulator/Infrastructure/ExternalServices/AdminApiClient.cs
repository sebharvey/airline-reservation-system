using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Infrastructure.ExternalServices;

internal sealed class AdminApiClient : IAdminApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string     _username;
    private readonly string     _password;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public AdminApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _username   = configuration["AdminApi:Username"] ?? throw new InvalidOperationException("AdminApi:Username is not configured.");
        _password   = configuration["AdminApi:Password"] ?? throw new InvalidOperationException("AdminApi:Password is not configured.");
    }

    public async Task<string> LoginAsync(CancellationToken ct = default)
    {
        var request  = new AdminLoginRequest(_username, _password);
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AdminLoginResponse>(JsonOptions, ct);
        return result?.AccessToken ?? throw new InvalidOperationException("Empty login response from Admin API.");
    }
}
