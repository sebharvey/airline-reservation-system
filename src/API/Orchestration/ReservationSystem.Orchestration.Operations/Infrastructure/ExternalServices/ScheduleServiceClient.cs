namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class ScheduleServiceClient
{
    private readonly HttpClient _httpClient;

    public ScheduleServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ScheduleMs");
    }

    // Methods will be implemented when business logic is built out
}
