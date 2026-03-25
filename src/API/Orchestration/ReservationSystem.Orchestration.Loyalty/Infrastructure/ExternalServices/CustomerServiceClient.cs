using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Customer microservice.
/// All calls use the "CustomerMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class CustomerServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CustomerServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CustomerMs");
    }

    public async Task<CreateCustomerDto> CreateCustomerAsync(
        string givenName,
        string surname,
        DateOnly? dateOfBirth,
        string preferredLanguage,
        CancellationToken cancellationToken = default)
    {
        var body = new { givenName, surname, dateOfBirth, preferredLanguage };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/customers", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await ReadErrorMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateCustomerDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Customer MS create customer.");
    }

    public async Task<CustomerDto?> GetCustomerByIdentityIdAsync(
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/customers/by-identity/{identityId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerDto>(JsonOptions, cancellationToken);
    }

    public async Task<CustomerDto?> GetCustomerAsync(
        string loyaltyNumber,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/customers/{loyaltyNumber}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerDto>(JsonOptions, cancellationToken);
    }

    public async Task<bool> UpdateCustomerAsync(
        string loyaltyNumber,
        object updateRequest,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync(
            $"/api/v1/customers/{loyaltyNumber}", updateRequest, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await ReadErrorMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task LinkIdentityAsync(
        string loyaltyNumber,
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        var body = new { identityId };
        var response = await _httpClient.PatchAsJsonAsync(
            $"/api/v1/customers/{loyaltyNumber}", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TransactionsDto?> GetTransactionsAsync(
        string loyaltyNumber,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/customers/{loyaltyNumber}/transactions?page={page}&pageSize={pageSize}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TransactionsDto>(JsonOptions, cancellationToken);
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiError = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(apiError?.Error))
                return apiError.Error;
        }
        catch
        {
            // Fall through to raw body read
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? "Validation failed." : raw;
    }
}
