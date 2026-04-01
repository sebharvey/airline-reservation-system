namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class BagServiceClient
{
    private readonly HttpClient _httpClient;

    public BagServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    // Methods will be implemented when business logic is built out
}
