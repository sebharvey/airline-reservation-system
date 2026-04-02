using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

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

    public async Task<List<IssuedTicket>> IssueTicketsAsync(
        Guid basketId, string bookingReference,
        List<TicketPassenger> passengers, List<TicketSegment> segments,
        CancellationToken ct)
    {
        var payload = new { basketId, bookingReference, passengers, segments };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/tickets", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Ticket issuance failed: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<IssueTicketsResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from ticket issuance.");
        return result.Tickets;
    }

    public async Task<List<AdminTicketRecord>> GetTicketsByBookingAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/tickets?bookingRef={Uri.EscapeDataString(bookingReference)}", ct);
        if (!response.IsSuccessStatusCode)
            return [];

        var result = await response.Content.ReadFromJsonAsync<List<AdminTicketRecord>>(JsonOptions, ct);
        return result ?? [];
    }

    public async Task CreateManifestAsync(string bookingReference, List<ManifestEntry> entries, CancellationToken ct)
    {
        var payload = new { bookingReference, entries };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/manifest", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Manifest creation failed: {error}");
        }
    }

    public async Task VoidTicketAsync(string eTicketNumber, CancellationToken ct)
    {
        using var content = new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/api/v1/tickets/{Uri.EscapeDataString(eTicketNumber)}/void", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Ticket void failed for {eTicketNumber}: {error}");
        }
    }

    public async Task DeleteManifestAsync(string bookingReference, string flightNumber, string departureDate, CancellationToken ct)
    {
        using var response = await _httpClient.DeleteAsync(
            $"/api/v1/manifest/{Uri.EscapeDataString(bookingReference)}/flight/{Uri.EscapeDataString(flightNumber)}/{Uri.EscapeDataString(departureDate)}", ct);
        // 404 is acceptable — manifest entry may not exist
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Manifest deletion failed: {error}");
        }
    }

    public async Task<List<IssuedTicket>> ReissueTicketsAsync(
        string bookingReference, string reason,
        List<TicketPassenger> passengers, List<TicketSegment> segments,
        CancellationToken ct)
    {
        var payload = new { bookingReference, reason, passengers, segments };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/tickets/reissue", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Ticket reissuance failed: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<IssueTicketsResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from ticket reissuance.");
        return result.Tickets;
    }

    public async Task<CreateBoardingCardsResult> CreateBoardingCardsAsync(
        string bookingReference,
        List<BoardingCardPassengerRequest> passengers,
        CancellationToken ct)
    {
        var payload = new { bookingReference, passengers };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/checkin", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Check-in failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<CreateBoardingCardsResult>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Empty response from check-in.");
    }

    public async Task<CreateBoardingCardsResult> GetBoardingCardsByBookingAsync(
        string bookingReference,
        CancellationToken ct)
    {
        var payload = new { bookingReference };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/boarding-cards", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Boarding card retrieval failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<CreateBoardingCardsResult>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Empty response from boarding card retrieval.");
    }

    public async Task IssueDocumentAsync(
        string bookingReference, string documentType, string passengerId,
        string inventoryId, decimal amount, string currency,
        CancellationToken ct)
    {
        var payload = new { bookingReference, documentType, passengerId, inventoryId, amount, currency };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/documents", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            // Document issuance failure is non-fatal — log but do not roll back
            System.Console.Error.WriteLine($"[DeliveryServiceClient] Document issuance failed: {error}");
        }
    }
}

public sealed class IssuedTicket
{
    [JsonPropertyName("ticketId")]
    public string TicketId { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("segmentIds")]
    public List<string> SegmentIds { get; init; } = [];
}

public sealed class TicketPassenger
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("formOfPayment")]
    public TicketFormOfPayment? FormOfPayment { get; init; }
}

public sealed class TicketFormOfPayment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("cardType")]
    public string? CardType { get; init; }

    [JsonPropertyName("maskedPan")]
    public string? MaskedPan { get; init; }

    [JsonPropertyName("expiryMmYy")]
    public string? ExpiryMmYy { get; init; }

    [JsonPropertyName("approvalCode")]
    public string? ApprovalCode { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;
}

public sealed class TicketSegment
{
    [JsonPropertyName("segmentId")]
    public string SegmentId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public string InventoryId { get; init; } = string.Empty;

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("fareBasisCode")]
    public string? FareBasisCode { get; init; }

    [JsonPropertyName("seatAssignments")]
    public List<SeatAssignmentItem> SeatAssignments { get; init; } = [];
}

public sealed class SeatAssignmentItem
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;
}

public sealed class ManifestEntry
{
    [JsonPropertyName("ticketId")]
    public string TicketId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public string InventoryId { get; init; } = string.Empty;

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;
}

public sealed class AdminTicketRecord
{
    [JsonPropertyName("ticketId")] public string TicketId { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("voidedAt")] public DateTime? VoidedAt { get; init; }
    [JsonPropertyName("ticketData")] public JsonElement? TicketData { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
    [JsonPropertyName("version")] public int Version { get; init; }
}

public sealed class BoardingCardPassengerRequest
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryIds")]
    public List<string> InventoryIds { get; init; } = [];
}

public sealed class CreateBoardingCardsResult
{
    [JsonPropertyName("boardingCards")]
    public List<BoardingCardItem> BoardingCards { get; init; } = [];
}

public sealed class BoardingCardItem
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDateTime")] public string DepartureDateTime { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("sequenceNumber")] public string SequenceNumber { get; init; } = string.Empty;
    [JsonPropertyName("bcbpString")] public string BcbpString { get; init; } = string.Empty;
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("gate")] public string Gate { get; init; } = string.Empty;
    [JsonPropertyName("boardingTime")] public string BoardingTime { get; init; } = string.Empty;
}

file sealed class IssueTicketsResult
{
    [JsonPropertyName("tickets")]
    public List<IssuedTicket> Tickets { get; init; } = [];
}
