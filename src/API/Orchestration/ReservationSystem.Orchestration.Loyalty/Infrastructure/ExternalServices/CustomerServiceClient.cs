namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

public sealed class CustomerServiceClient
{
    private readonly HttpClient _httpClient;

    public CustomerServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CustomerMs");
    }

    // Methods will be implemented when business logic is built out
}
