using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

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
}
