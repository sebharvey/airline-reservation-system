using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Identity microservice.
/// All calls use the "IdentityMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class IdentityServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IdentityServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("IdentityMs");
    }

    public async Task<IdentityLoginResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var body = new { email, password };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IdentityLoginResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Identity MS login.");
    }

    public async Task<IdentityRefreshTokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var body = new { refreshToken };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/refresh", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IdentityRefreshTokenResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Identity MS refresh.");
    }

    public async Task<IdentityVerifyTokenResponse?> VerifyTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var body = new { accessToken };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/verify", body, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IdentityVerifyTokenResponse>(JsonOptions, cancellationToken);
    }

    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var body = new { refreshToken };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/logout", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IdentityCreateAccountResponse> CreateAccountAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var body = new { email, password };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/accounts", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("An account with this email address is already registered.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IdentityCreateAccountResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Identity MS create account.");
    }

    public async Task VerifyEmailAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"/api/v1/accounts/{userAccountId}/verify-email",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"No user account found for ID '{userAccountId}'.");

        response.EnsureSuccessStatusCode();
    }
}
