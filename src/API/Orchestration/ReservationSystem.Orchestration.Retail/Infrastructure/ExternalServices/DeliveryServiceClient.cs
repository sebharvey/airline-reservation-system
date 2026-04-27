using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

public sealed class DeliveryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeliveryServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeliveryServiceClient(IHttpClientFactory httpClientFactory, ILogger<DeliveryServiceClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("DeliveryMs");
        _logger = logger;
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

    // TODO: Remove — temporary debug method
    public async Task<string?> GetTicketsDebugRawAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/debug/tickets?bookingRef={Uri.EscapeDataString(bookingReference)}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string?> IssueDocumentAsync(
        string bookingReference, string documentType, string passengerId,
        string segmentRef, decimal amount, string currency,
        string? paymentReference,
        CancellationToken ct)
    {
        var payload = new { bookingReference, documentType, passengerId, segmentRef, amount, currencyCode = currency, paymentReference };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/documents", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            _logger.LogError("[DeliveryServiceClient] Document issuance failed: {Error}", error);
            return null;
        }
        try
        {
            var result = await response.Content.ReadFromJsonAsync<DocumentIssuanceResult>(JsonOptions, ct);
            return result?.DocumentNumber;
        }
        catch { return null; }
    }

    private sealed class DocumentIssuanceResult
    {
        public string DocumentNumber { get; init; } = string.Empty;
        public string DocumentType { get; init; } = string.Empty;
    }

    public async Task<List<AdminDocumentRecord>> GetDocumentsByBookingAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/documents?bookingRef={Uri.EscapeDataString(bookingReference)}", ct);
        if (!response.IsSuccessStatusCode)
            return [];

        var result = await response.Content.ReadFromJsonAsync<List<AdminDocumentRecord>>(JsonOptions, ct);
        return result ?? [];
    }

    // TODO: Remove — temporary debug method
    public async Task<string?> GetDocumentsDebugRawAsync(string bookingReference, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/debug/documents?bookingRef={Uri.EscapeDataString(bookingReference)}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<bool> UpdateManifestSeatAsync(string eTicketNumber, string? newSeatNumber, CancellationToken ct)
    {
        var payload = new { seatNumber = newSeatNumber };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync(
            $"/api/v1/manifest/{Uri.EscapeDataString(eTicketNumber)}/seat", content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<AdminFlightManifestResult?> GetManifestByFlightAsync(
        string flightNumber, string departureDate, CancellationToken ct)
    {
        var url = $"/api/v1/manifest?flightNumber={Uri.EscapeDataString(flightNumber)}&departureDate={Uri.EscapeDataString(departureDate)}";
        using var response = await _httpClient.GetAsync(url, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new AdminFlightManifestResult { Entries = [] };
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<AdminFlightManifestResult>(JsonOptions, ct);
    }

    public async Task WriteManifestAsync(
        string bookingReference,
        Guid orderId,
        Guid inventoryId,
        string flightNumber,
        string origin,
        string destination,
        string departureDate,
        string aircraftType,
        string departureTime,
        string arrivalTime,
        string bookingType,
        List<ManifestPassengerEntry> entries,
        CancellationToken ct)
    {
        var payload = new { bookingReference, orderId, inventoryId, flightNumber, origin, destination, departureDate, aircraftType, departureTime, arrivalTime, bookingType, entries };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/manifest", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"Manifest write failed for booking {bookingReference}: {error}");
        }
    }

    public async Task<AdminOciCheckInResult> OciCheckInAsync(
        string departureAirport,
        IReadOnlyList<AdminOciCheckInTicket> tickets,
        CancellationToken ct,
        bool bypassTimatic = false)
    {
        var payload = new { departureAirport, tickets, bypassTimatic };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/oci/checkin", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var blocked = await response.Content.ReadFromJsonAsync<AdminOciTimaticBlockedResponse>(JsonOptions, ct);
                if (blocked?.TimaticNotes is { Count: > 0 })
                    throw new AdminOciTimaticBlockedException(blocked.Error ?? "OCI check-in blocked by Timatic.", blocked.TimaticNotes);
            }
            catch (AdminOciTimaticBlockedException) { throw; }
            catch { }

            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"OCI check-in failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<AdminOciCheckInResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from OCI check-in.");
    }

    public async Task<AdminOciBoardingDocsResult> GetOciBoardingDocsAsync(
        string departureAirport,
        IReadOnlyList<string> ticketNumbers,
        CancellationToken ct)
    {
        var payload = new { departureAirport, ticketNumbers };
        using var response = await _httpClient.PostAsJsonAsync("/api/v1/oci/boarding-docs", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.ReadErrorMessageAsync(ct);
            throw new InvalidOperationException($"OCI boarding-docs failed: {error}");
        }
        return await response.Content.ReadFromJsonAsync<AdminOciBoardingDocsResult>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from OCI boarding-docs.");
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

    [JsonPropertyName("dob")]
    public string? Dob { get; init; }

    [JsonPropertyName("fareConstruction")]
    public TicketFareConstruction? FareConstruction { get; init; }

    [JsonPropertyName("formOfPayment")]
    public TicketFormOfPayment? FormOfPayment { get; init; }
}

public sealed class TicketFareConstruction
{
    [JsonPropertyName("pricingCurrency")]
    public string PricingCurrency { get; init; } = string.Empty;

    [JsonPropertyName("collectingCurrency")]
    public string CollectingCurrency { get; init; } = string.Empty;

    [JsonPropertyName("baseFare")]
    public decimal BaseFare { get; init; }

    [JsonPropertyName("equivalentFarePaid")]
    public decimal EquivalentFarePaid { get; init; }

    [JsonPropertyName("nucAmount")]
    public decimal NucAmount { get; init; }

    [JsonPropertyName("roeApplied")]
    public decimal RoeApplied { get; init; }

    [JsonPropertyName("fareCalculationLine")]
    public string FareCalculationLine { get; init; } = string.Empty;

    [JsonPropertyName("taxes")]
    public List<TicketTaxLine> Taxes { get; init; } = [];

    [JsonPropertyName("totalTaxes")]
    public decimal TotalTaxes { get; init; }
}

public sealed class TicketTaxLine
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
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

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("seatAssignments")]
    public List<SeatAssignmentItem> SeatAssignments { get; init; } = [];

    [JsonPropertyName("ssrCodes")]
    public List<SegmentSsrCode> SsrCodes { get; init; } = [];
}

public sealed class SegmentSsrCode
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("segmentRef")]
    public string SegmentRef { get; init; } = string.Empty;
}

public sealed class SeatAssignmentItem
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;
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

public sealed class AdminDocumentRecord
{
    [JsonPropertyName("documentId")] public Guid DocumentId { get; init; }
    [JsonPropertyName("documentNumber")] public string DocumentNumber { get; init; } = string.Empty;
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string? ETicketNumber { get; init; }
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("segmentRef")] public string SegmentRef { get; init; } = string.Empty;
    [JsonPropertyName("paymentReference")] public string PaymentReference { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; init; } = string.Empty;
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("documentData")] public JsonElement? DocumentData { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
}

public sealed class AdminOciCheckInTicket
{
    [JsonPropertyName("ticketNumber")] public string TicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("docNationality")] public string? DocNationality { get; init; }
    [JsonPropertyName("docNumber")] public string? DocNumber { get; init; }
    [JsonPropertyName("docIssuingCountry")] public string? DocIssuingCountry { get; init; }
    [JsonPropertyName("docExpiryDate")] public string? DocExpiryDate { get; init; }
}

public sealed class AdminOciCheckInResult
{
    [JsonPropertyName("checkedIn")] public int CheckedIn { get; init; }
    [JsonPropertyName("tickets")] public List<AdminOciCheckedInTicket> Tickets { get; init; } = [];
    [JsonPropertyName("timaticNotes")] public List<AdminOciTimaticNote> TimaticNotes { get; init; } = [];
}

public sealed class AdminOciCheckedInTicket
{
    [JsonPropertyName("ticketNumber")] public string TicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
}

public sealed class AdminOciBoardingDocsResult
{
    [JsonPropertyName("boardingCards")] public List<AdminOciBoardingCard> BoardingCards { get; init; } = [];
}

public sealed class AdminOciBoardingCard
{
    [JsonPropertyName("ticketNumber")] public string TicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("sequenceNumber")] public string SequenceNumber { get; init; } = string.Empty;
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("bcbpString")] public string BcbpString { get; init; } = string.Empty;
}

public sealed class AdminOciTimaticNote
{
    [JsonPropertyName("checkType")] public string CheckType { get; init; } = string.Empty;
    [JsonPropertyName("ticketNumber")] public string TicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("timestamp")] public string Timestamp { get; init; } = string.Empty;
}

public sealed class AdminOciTimaticBlockedException : Exception
{
    public IReadOnlyList<AdminOciTimaticNote> TimaticNotes { get; }
    public AdminOciTimaticBlockedException(string message, IReadOnlyList<AdminOciTimaticNote> notes)
        : base(message) => TimaticNotes = notes;
}

file sealed class AdminOciTimaticBlockedResponse
{
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("timaticNotes")] public List<AdminOciTimaticNote> TimaticNotes { get; init; } = [];
}

file sealed class IssueTicketsResult
{
    [JsonPropertyName("tickets")]
    public List<IssuedTicket> Tickets { get; init; } = [];
}

public sealed class AdminFlightManifestResult
{
    [JsonPropertyName("entries")]
    public List<AdminManifestEntry> Entries { get; init; } = [];
}

public sealed class AdminManifestEntry
{
    [JsonPropertyName("orderId")]          public Guid OrderId            { get; init; }
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")]      public string PassengerId      { get; init; } = string.Empty;
    [JsonPropertyName("givenName")]        public string GivenName        { get; init; } = string.Empty;
    [JsonPropertyName("surname")]          public string Surname          { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")]    public string ETicketNumber    { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")]       public string? SeatNumber      { get; init; }
    [JsonPropertyName("cabinCode")]        public string CabinCode        { get; init; } = string.Empty;
    [JsonPropertyName("bookingType")]      public string BookingType      { get; init; } = string.Empty;
    [JsonPropertyName("checkedIn")]        public bool CheckedIn          { get; init; }
    [JsonPropertyName("ssrCodes")]         public List<string> SsrCodes   { get; init; } = [];
}

public sealed class ManifestPassengerEntry
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatPosition")]
    public string? SeatPosition { get; init; }
}
