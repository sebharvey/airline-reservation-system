using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the Customer microservice.
/// All calls use the "CustomerMs" named HttpClient configured in Program.cs,
/// which carries the x-functions-key header for service-to-service auth.
/// </summary>
public sealed class CustomerServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = SharedJsonOptions.CamelCaseIgnoreNull;

    public CustomerServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CustomerMs");
    }

    public async Task<CreateCustomerDto> CreateCustomerAsync(
        string givenName,
        string surname,
        DateOnly? dateOfBirth,
        string preferredLanguage,
        string? phoneNumber = null,
        string? nationality = null,
        CancellationToken cancellationToken = default)
    {
        var body = new { givenName, surname, dateOfBirth, preferredLanguage, phoneNumber, nationality };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/customers", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

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
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

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

    public async Task<CustomerPreferencesDto?> GetPreferencesAsync(
        string loyaltyNumber,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/customers/{loyaltyNumber}/preferences", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerPreferencesDto>(JsonOptions, cancellationToken);
    }

    public async Task<bool> UpdatePreferencesAsync(
        string loyaltyNumber,
        object preferencesRequest,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"/api/v1/customers/{loyaltyNumber}/preferences", preferencesRequest, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteCustomerAsync(
        string loyaltyNumber,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"/api/v1/customers/{loyaltyNumber}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task AddPointsAsync(
        string loyaltyNumber,
        int points,
        string transactionType,
        string description,
        CancellationToken cancellationToken = default)
    {
        var body = new { points, transactionType, description };
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/customers/{loyaltyNumber}/points/add", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Customer '{loyaltyNumber}' not found when awarding points.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.ReadErrorMessageAsync(cancellationToken));

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

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var body = new { query = query ?? string.Empty };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/customers/search", body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<CustomerDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<TransferPointsResultDto?> TransferPointsAsync(
        string senderLoyaltyNumber,
        string recipientLoyaltyNumber,
        int points,
        CancellationToken cancellationToken = default)
    {
        var body = new { recipientLoyaltyNumber, points };
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/customers/{senderLoyaltyNumber}/points/transfer", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await response.ReadErrorMessageAsync(cancellationToken));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TransferPointsResultDto>(JsonOptions, cancellationToken);
    }
}
