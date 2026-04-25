using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

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

    public async Task<ManifestResponse> GetManifestAsync(string flightNumber, string departureDate, CancellationToken ct)
    {
        var url = $"/api/v1/manifest?flightNumber={Uri.EscapeDataString(flightNumber)}&departureDate={departureDate}";
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ManifestResponse();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ManifestResponse>(JsonOptions, ct)
            ?? new ManifestResponse();
    }

    public async Task DeleteManifestFlightAsync(string bookingReference, string flightNumber, string departureDate, CancellationToken ct)
    {
        var url = $"/api/v1/manifest/{Uri.EscapeDataString(bookingReference)}/flight/{Uri.EscapeDataString(flightNumber)}/{departureDate}";
        using var response = await _httpClient.DeleteAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return; // already removed — treat as success

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to delete manifest for {bookingReference}/{flightNumber}/{departureDate}: {await response.ReadErrorMessageAsync(ct)}");
    }

    public async Task<ReissueTicketsResponse> ReissueTicketsAsync(ReissueTicketsRequest request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/tickets/reissue", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to reissue tickets for {request.BookingReference}: {await response.ReadErrorMessageAsync(ct)}");

        return await response.Content.ReadFromJsonAsync<ReissueTicketsResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from ticket reissue.");
    }

    public async Task WriteManifestAsync(WriteManifestRequest request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/manifest", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to write manifest for {request.BookingReference}: {await response.ReadErrorMessageAsync(ct)}");
    }

    public async Task RebookManifestAsync(
        string bookingReference,
        string fromFlightNumber,
        string fromDepartureDate,
        RebookManifestRequest request,
        CancellationToken ct)
    {
        var url = $"/api/v1/manifest/{Uri.EscapeDataString(bookingReference)}/flight/{Uri.EscapeDataString(fromFlightNumber)}/{fromDepartureDate}";
        using var response = await _httpClient.PatchAsJsonAsync(url, request, JsonOptions, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // No manifest entries found — log but don't fail the overall rebook
            return;
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to rebook manifest for {bookingReference}/{fromFlightNumber}/{fromDepartureDate}: {await response.ReadErrorMessageAsync(ct)}");
    }

    public async Task<IReadOnlyList<BookingTicketDto>> GetTicketsByBookingAsync(string bookingReference, CancellationToken ct)
    {
        var url = $"/api/v1/tickets?bookingRef={Uri.EscapeDataString(bookingReference)}";
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<BookingTicketDto>>(JsonOptions, ct)
            ?? [];
    }

    public async Task VoidTicketAsync(string eTicketNumber, string reason, CancellationToken ct)
    {
        var payload = new { reason, actor = "OperationsAPI" };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/tickets/{Uri.EscapeDataString(eTicketNumber)}/void", content, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to void ticket {eTicketNumber}: {await response.ReadErrorMessageAsync(ct)}");
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

    [JsonPropertyName("docNationality")]
    public string? DocNationality { get; init; }

    [JsonPropertyName("docNumber")]
    public string? DocNumber { get; init; }

    [JsonPropertyName("docIssuingCountry")]
    public string? DocIssuingCountry { get; init; }

    [JsonPropertyName("docExpiryDate")]
    public string? DocExpiryDate { get; init; }
}

public sealed class OciCheckInResult
{
    [JsonPropertyName("checkedIn")]
    public int CheckedIn { get; init; }

    [JsonPropertyName("tickets")]
    public List<OciCheckedInTicket> Tickets { get; init; } = [];

    [JsonPropertyName("timaticNotes")]
    public List<DeliveryTimaticNote> TimaticNotes { get; init; } = [];
}

public sealed class DeliveryTimaticNote
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
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
