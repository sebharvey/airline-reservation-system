namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class PaymentServiceClient
{
    private readonly HttpClient _httpClient;

    public PaymentServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("PaymentMs");
    }

    // Methods will be implemented when business logic is built out
}
