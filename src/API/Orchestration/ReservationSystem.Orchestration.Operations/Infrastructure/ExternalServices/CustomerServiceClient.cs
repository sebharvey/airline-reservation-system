using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

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

    /// <summary>
    /// Retrieve a customer profile by loyalty number via Customer MS GET /v1/customers/{loyaltyNumber}.
    /// Returns null if not found.
    /// </summary>
    public async Task<CustomerProfile?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/customers/{Uri.EscapeDataString(loyaltyNumber)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CustomerProfile>(JsonOptions, ct);
    }
}

public sealed class CustomerProfile
{
    [JsonPropertyName("customerId")]
    public string CustomerId { get; init; } = string.Empty;

    [JsonPropertyName("loyaltyNumber")]
    public string LoyaltyNumber { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; init; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; init; }

    [JsonPropertyName("passportIssueDate")]
    public string? PassportIssueDate { get; init; }

    [JsonPropertyName("passportIssuer")]
    public string? PassportIssuer { get; init; }

    [JsonPropertyName("passportExpiryDate")]
    public string? PassportExpiryDate { get; init; }
}
