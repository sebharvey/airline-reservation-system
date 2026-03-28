using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Json;
using System.Net;
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

    public async Task<IReadOnlyList<UserMsResponse>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/users", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<UserMsResponse>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<UserMsResponse?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/users/{userId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserMsResponse>(JsonOptions, cancellationToken);
    }

    public async Task<AddUserMsResponse> CreateUserAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ArgumentException(error);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(error);
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AddUserMsResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from User MS create.");
    }

    public async Task<bool> UpdateUserAsync(
        Guid userId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/users/{userId}", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ArgumentException(error);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(error);
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> SetUserStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var body = new { isActive };
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/users/{userId}/status", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> UnlockUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"/api/v1/users/{userId}/unlock", null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var body = new { newPassword };
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/users/{userId}/reset-password", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ArgumentException(error);
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/users/{userId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
