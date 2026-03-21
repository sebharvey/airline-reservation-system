namespace ReservationSystem.Orchestration.Disruption.Infrastructure.ExternalServices;

public sealed class OrderServiceClient
{
    private readonly HttpClient _httpClient;

    public OrderServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OrderMs");
    }

    // Methods will be implemented when business logic is built out
}
