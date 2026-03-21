namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class OfferServiceClient
{
    private readonly HttpClient _httpClient;

    public OfferServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OfferMs");
    }

    // Methods will be implemented when business logic is built out
}
