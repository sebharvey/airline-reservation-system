using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class DeliveryServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeliveryServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("DeliveryMs");
    }

    /// <summary>
    /// Check in a set of tickets for a departure airport via Delivery MS POST /v1/oci/checkin.
    /// </summary>
    public async Task<OciCheckInResult> CheckInAsync(string departureAirport, IReadOnlyList<OciCheckInTicket> tickets, CancellationToken ct)
    {
        var payload = new { departureAirport, tickets };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/oci/checkin", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"OCI check-in failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OciCheckInResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from OCI check-in.");
    }

    /// <summary>
    /// Generate boarding documents for checked-in tickets via Delivery MS POST /v1/oci/boarding-docs.
    /// </summary>
    public async Task<OciBoardingDocsResult> GetBoardingDocsAsync(string departureAirport, IReadOnlyList<string> ticketNumbers, CancellationToken ct)
    {
        var payload = new { departureAirport, ticketNumbers };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/oci/boarding-docs", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"OCI boarding-docs failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<OciBoardingDocsResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from OCI boarding-docs.");
    }
}

public sealed class OciCheckInTicket
{
    [JsonPropertyName("ticketNumber")]
    public string TicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;
}

public sealed class OciCheckInResult
{
    [JsonPropertyName("checkedIn")]
    public int CheckedIn { get; init; }

    [JsonPropertyName("tickets")]
    public List<OciCheckedInTicket> Tickets { get; init; } = [];
}

public sealed class OciCheckedInTicket
{
    [JsonPropertyName("ticketNumber")]
    public string TicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed class OciBoardingDocsResult
{
    [JsonPropertyName("boardingCards")]
    public List<OciBoardingCard> BoardingCards { get; init; } = [];
}

public sealed class OciBoardingCard
{
    [JsonPropertyName("ticketNumber")]
    public string TicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("sequenceNumber")]
    public string SequenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("bcbpString")]
    public string BcbpString { get; init; } = string.Empty;
}
