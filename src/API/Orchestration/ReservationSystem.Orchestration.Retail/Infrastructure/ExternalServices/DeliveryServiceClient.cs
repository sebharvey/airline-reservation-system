namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class DeliveryServiceClient
{
    private readonly HttpClient _httpClient;

    public DeliveryServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("DeliveryMs");
    }

    // Methods will be implemented when business logic is built out
}
