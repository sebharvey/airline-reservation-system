namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class SeatServiceClient
{
    private readonly HttpClient _httpClient;

    public SeatServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("SeatMs");
    }

    // Methods will be implemented when business logic is built out
}
