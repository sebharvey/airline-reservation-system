namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

public sealed class IdentityServiceClient
{
    private readonly HttpClient _httpClient;

    public IdentityServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("IdentityMs");
    }

    // Methods will be implemented when business logic is built out
}
