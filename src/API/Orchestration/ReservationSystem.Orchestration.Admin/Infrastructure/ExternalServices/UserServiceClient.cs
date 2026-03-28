using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Json;
using System.Net.Http.Json;

namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the User microservice.
/// All calls use the "UserMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class UserServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCaseIgnoreNull;

    public UserServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("UserMs");
    }

    public async Task<UserLoginResponse> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var body = new { username, password };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users/login", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UserLoginResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from User MS login.");
    }
}
