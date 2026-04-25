using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.ExternalServices;

public sealed class TimaticServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public TimaticServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("Timatic");
    }

    public async Task<TimaticDocumentCheckResult> DocumentCheckAsync(TimaticDocumentCheckRequest request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/autocheck/v1/documentcheck", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Timatic document check failed: HTTP {(int)response.StatusCode}");
        return await response.Content.ReadFromJsonAsync<TimaticDocumentCheckResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Timatic document check.");
    }

    public async Task<TimaticApisCheckResult> ApisCheckAsync(TimaticApisCheckRequest request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/autocheck/v1/apischeck", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Timatic APIS check failed: HTTP {(int)response.StatusCode}");
        return await response.Content.ReadFromJsonAsync<TimaticApisCheckResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Timatic APIS check.");
    }
}

// ── Document check ────────────────────────────────────────────────────────────

public sealed class TimaticDocumentCheckRequest
{
    [JsonPropertyName("transactionIdentifier")]
    public string TransactionIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("airlineCode")]
    public string AirlineCode { get; init; } = "AX";

    [JsonPropertyName("journeyType")]
    public string JourneyType { get; init; } = "OW";

    [JsonPropertyName("paxInfo")]
    public TimaticDocCheckPaxInfo PaxInfo { get; init; } = new();

    [JsonPropertyName("itinerary")]
    public List<TimaticItinerarySegment> Itinerary { get; init; } = [];
}

public sealed class TimaticDocCheckPaxInfo
{
    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = "P";

    [JsonPropertyName("nationality")]
    public string Nationality { get; init; } = string.Empty;

    [JsonPropertyName("documentIssuerCountry")]
    public string DocumentIssuerCountry { get; init; } = string.Empty;

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; init; } = string.Empty;

    [JsonPropertyName("documentExpiryDate")]
    public string DocumentExpiryDate { get; init; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("gender")]
    public string Gender { get; init; } = "X";

    [JsonPropertyName("residentCountry")]
    public string ResidentCountry { get; init; } = string.Empty;
}

public sealed class TimaticItinerarySegment
{
    [JsonPropertyName("departureAirport")]
    public string DepartureAirport { get; init; } = string.Empty;

    [JsonPropertyName("arrivalAirport")]
    public string ArrivalAirport { get; init; } = string.Empty;

    [JsonPropertyName("airline")]
    public string Airline { get; init; } = "AX";

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;
}

public sealed class TimaticDocumentCheckResult
{
    [JsonPropertyName("transactionIdentifier")]
    public string TransactionIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("requirements")]
    public List<TimaticRequirement> Requirements { get; init; } = [];

    [JsonPropertyName("advisories")]
    public List<TimaticAdvisory> Advisories { get; init; } = [];
}

public sealed class TimaticRequirement
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; init; }
}

public sealed class TimaticAdvisory
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; init; }
}

// ── APIS check ────────────────────────────────────────────────────────────────

public sealed class TimaticApisCheckRequest
{
    [JsonPropertyName("transactionIdentifier")]
    public string TransactionIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("airlineCode")]
    public string AirlineCode { get; init; } = "AX";

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureAirport")]
    public string DepartureAirport { get; init; } = string.Empty;

    [JsonPropertyName("arrivalAirport")]
    public string ArrivalAirport { get; init; } = string.Empty;

    [JsonPropertyName("paxInfo")]
    public TimaticApisPaxInfo PaxInfo { get; init; } = new();
}

public sealed class TimaticApisPaxInfo
{
    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("givenNames")]
    public string GivenNames { get; init; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("gender")]
    public string Gender { get; init; } = "X";

    [JsonPropertyName("nationality")]
    public string Nationality { get; init; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = "P";

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; init; } = string.Empty;

    [JsonPropertyName("documentIssuerCountry")]
    public string DocumentIssuerCountry { get; init; } = string.Empty;

    [JsonPropertyName("documentExpiryDate")]
    public string DocumentExpiryDate { get; init; } = string.Empty;
}

public sealed class TimaticApisCheckResult
{
    [JsonPropertyName("transactionIdentifier")]
    public string TransactionIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("apisStatus")]
    public string ApisStatus { get; init; } = string.Empty;

    [JsonPropertyName("fineRisk")]
    public string FineRisk { get; init; } = string.Empty;

    [JsonPropertyName("warnings")]
    public List<TimaticApisWarning> Warnings { get; init; } = [];

    [JsonPropertyName("auditRef")]
    public string AuditRef { get; init; } = string.Empty;
}

public sealed class TimaticApisWarning
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
