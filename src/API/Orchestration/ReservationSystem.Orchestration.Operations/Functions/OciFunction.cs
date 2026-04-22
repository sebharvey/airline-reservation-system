using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.OciCheckIn;
using ReservationSystem.Orchestration.Operations.Application.OciPax;
using ReservationSystem.Orchestration.Operations.Application.OciRetrieve;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for the Online Check-In (OCI) journey.
/// Orchestrates calls across Order MS, Delivery MS, and Customer MS.
/// </summary>
public sealed class OciFunction
{
    private readonly OciRetrieveHandler _retrieveHandler;
    private readonly OciPaxHandler _paxHandler;
    private readonly OciCheckInHandler _checkInHandler;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<OciFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // IATA e-ticket format: 3-digit airline code, hyphen, 10-digit sequential number
    private static readonly Regex TicketNumberRegex =
        new(@"^\d{3}-\d{10}$", RegexOptions.Compiled);

    public OciFunction(
        OciRetrieveHandler retrieveHandler,
        OciPaxHandler paxHandler,
        OciCheckInHandler checkInHandler,
        DeliveryServiceClient deliveryServiceClient,
        OrderServiceClient orderServiceClient,
        ILogger<OciFunction> logger)
    {
        _retrieveHandler = retrieveHandler;
        _paxHandler = paxHandler;
        _checkInHandler = checkInHandler;
        _deliveryServiceClient = deliveryServiceClient;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/retrieve
    // -------------------------------------------------------------------------

    [Function("OciRetrieve")]
    [OpenApiOperation(operationId: "OciRetrieve", tags: new[] { "OCI" },
        Summary = "Retrieve booking for online check-in by booking reference and lead PAX name")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, firstName, lastName, departureAirport }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> OciRetrieve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/retrieve")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("bookingReference", out var brEl) || string.IsNullOrWhiteSpace(brEl.GetString()))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (!body.TryGetProperty("lastName", out var lnEl) || string.IsNullOrWhiteSpace(lnEl.GetString()))
            return await req.BadRequestAsync("'lastName' is required.");

        var bookingReference = brEl.GetString()!.ToUpperInvariant().Trim();
        var firstName = body.TryGetProperty("firstName", out var fnEl) ? fnEl.GetString() ?? "" : "";
        var lastName = lnEl.GetString()!.Trim();
        var departureAirport = body.TryGetProperty("departureAirport", out var daEl) ? daEl.GetString() ?? "" : "";
        var loyaltyNumber = body.TryGetProperty("loyaltyNumber", out var loyEl) ? loyEl.GetString() : null;

        try
        {
            var query = new OciRetrieveQuery(bookingReference, firstName, lastName, departureAirport, loyaltyNumber);
            var result = await _retrieveHandler.HandleAsync(query, ct);

            if (result is null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            return await req.OkJsonAsync(new
            {
                bookingReference = result.BookingReference,
                checkInEligible = result.CheckInEligible,
                isStandby = result.IsStandby,
                passengers = result.Passengers.Select(p => new
                {
                    passengerId = p.PassengerId,
                    ticketNumber = p.TicketNumber,
                    givenName = p.GivenName,
                    surname = p.Surname,
                    passengerTypeCode = p.PassengerTypeCode,
                    travelDocument = p.TravelDocument is not null ? new
                    {
                        type = p.TravelDocument.Type,
                        number = p.TravelDocument.Number,
                        issuingCountry = p.TravelDocument.IssuingCountry,
                        nationality = p.TravelDocument.Nationality,
                        issueDate = p.TravelDocument.IssueDate,
                        expiryDate = p.TravelDocument.ExpiryDate
                    } : (object?)null
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("OCI retrieve: booking not fully ticketed for {BookingReference} — {Message}", bookingReference, ex.Message);
            return await req.UnprocessableEntityAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCI retrieve failed for {BookingReference}", bookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/pax
    // -------------------------------------------------------------------------

    [Function("OciPax")]
    [OpenApiOperation(operationId: "OciPax", tags: new[] { "OCI" },
        Summary = "Submit or update passport details for each PAX and perform check-in")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, departureAirport, passengers: [{ ticketNumber, travelDocument }] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> OciPax(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/pax")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("bookingReference", out var brEl) || string.IsNullOrWhiteSpace(brEl.GetString()))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (!body.TryGetProperty("passengers", out var paxEl) || paxEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'passengers' array is required.");

        var bookingReference = brEl.GetString()!.ToUpperInvariant().Trim();
        var departureAirport = body.TryGetProperty("departureAirport", out var daEl) ? daEl.GetString() ?? "" : "";

        var passengers = new List<OciPaxPassenger>();
        foreach (var p in paxEl.EnumerateArray())
        {
            var ticketNumber = p.TryGetProperty("ticketNumber", out var tnEl) ? tnEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(ticketNumber)) continue;

            if (!TicketNumberRegex.IsMatch(ticketNumber))
                return await req.BadRequestAsync($"Ticket number '{ticketNumber}' is not a valid format. Expected: NNN-NNNNNNNNNN (e.g. 932-0000000001).");

            if (!p.TryGetProperty("travelDocument", out var tdEl) || tdEl.ValueKind != JsonValueKind.Object)
                continue;

            var type = tdEl.TryGetProperty("type", out var tyEl) ? tyEl.GetString() ?? "" : "";
            var number = tdEl.TryGetProperty("number", out var numEl) ? numEl.GetString() ?? "" : "";
            var issuingCountry = tdEl.TryGetProperty("issuingCountry", out var icEl) ? icEl.GetString() ?? "" : "";
            var nationality = tdEl.TryGetProperty("nationality", out var natEl) ? natEl.GetString() ?? "" : "";
            var issueDate = tdEl.TryGetProperty("issueDate", out var idEl) ? idEl.GetString() ?? "" : "";
            var expiryDate = tdEl.TryGetProperty("expiryDate", out var edEl) ? edEl.GetString() ?? "" : "";

            // Validate passport expiry
            if (DateOnly.TryParse(expiryDate, out var expiry) && expiry < DateOnly.FromDateTime(DateTime.UtcNow))
                return await req.BadRequestAsync($"Passport for ticket {ticketNumber} has expired ({expiryDate}).");

            passengers.Add(new OciPaxPassenger(
                ticketNumber,
                new OciPaxTravelDocument(type, number, issuingCountry, nationality, issueDate, expiryDate)));
        }

        if (passengers.Count == 0)
            return await req.BadRequestAsync("At least one valid passenger with travel document is required.");

        try
        {
            var command = new OciPaxCommand(bookingReference, departureAirport, passengers);
            var result = await _paxHandler.HandleAsync(command, ct);

            if (result is null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            if (!result.Success)
            {
                if (result.ErrorMessage is not null)
                    return await req.BadRequestAsync(result.ErrorMessage);
                return await req.InternalServerErrorAsync();
            }

            return await req.OkJsonAsync(new
            {
                bookingReference = result.BookingReference,
                success = result.Success
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCI pax failed for {BookingReference}", bookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/seats
    // -------------------------------------------------------------------------

    [Function("OciSeats")]
    [OpenApiOperation(operationId: "OciSeats", tags: new[] { "OCI" },
        Summary = "Submit seat selection for check-in (not implemented — returns success)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, departureAirport }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    public async Task<HttpResponseData> OciSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/seats")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var bookingReference = body.TryGetProperty("bookingReference", out var brEl) ? brEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(bookingReference))
            return await req.BadRequestAsync("'bookingReference' is required.");

        var ref1 = bookingReference.ToUpperInvariant().Trim();
        var docError1 = await GetTravelDocumentErrorAsync(ref1, ct);
        if (docError1 is not null)
            return await req.BadRequestAsync(docError1);

        // Seat selection is not implemented at this time — return success
        return await req.OkJsonAsync(new { bookingReference = ref1, success = true });
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/bags
    // -------------------------------------------------------------------------

    [Function("OciBags")]
    [OpenApiOperation(operationId: "OciBags", tags: new[] { "OCI" },
        Summary = "Submit baggage selection for check-in (not implemented — returns success)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, departureAirport }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    public async Task<HttpResponseData> OciBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/bags")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var bookingReference = body.TryGetProperty("bookingReference", out var brEl) ? brEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(bookingReference))
            return await req.BadRequestAsync("'bookingReference' is required.");

        var ref2 = bookingReference.ToUpperInvariant().Trim();
        var docError2 = await GetTravelDocumentErrorAsync(ref2, ct);
        if (docError2 is not null)
            return await req.BadRequestAsync(docError2);

        // Baggage selection is not implemented at this time — return success
        return await req.OkJsonAsync(new { bookingReference = ref2, success = true });
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/checkin
    // -------------------------------------------------------------------------

    [Function("OciCheckIn")]
    [OpenApiOperation(operationId: "OciCheckIn", tags: new[] { "OCI" },
        Summary = "Complete check-in for all passengers on a booking; calls the Delivery MS to update coupon status to C")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, departureAirport }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> OciCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/checkin")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("bookingReference", out var brEl) || string.IsNullOrWhiteSpace(brEl.GetString()))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (!body.TryGetProperty("departureAirport", out var daEl) || string.IsNullOrWhiteSpace(daEl.GetString()))
            return await req.BadRequestAsync("'departureAirport' is required.");

        var bookingReference = brEl.GetString()!.ToUpperInvariant().Trim();
        var departureAirport = daEl.GetString()!.ToUpperInvariant().Trim();

        var docError = await GetTravelDocumentErrorAsync(bookingReference, ct);
        if (docError is not null)
            return await req.BadRequestAsync(docError);

        try
        {
            var command = new OciCheckInCommand(bookingReference, departureAirport);
            var result = await _checkInHandler.HandleAsync(command, ct);

            if (result is null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            return await req.OkJsonAsync(new
            {
                bookingReference = result.BookingReference,
                checkedIn = result.CheckedIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCI check-in failed for {BookingReference}", bookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/boarding-docs
    // -------------------------------------------------------------------------

    [Function("OciBoardingDocs")]
    [OpenApiOperation(operationId: "OciBoardingDocs", tags: new[] { "OCI" },
        Summary = "Generate boarding documents for checked-in tickets at a departure airport")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ departureAirport, ticketNumbers: [\"932-...\"] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> OciBoardingDocs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/boarding-docs")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("departureAirport", out var airportEl) || string.IsNullOrWhiteSpace(airportEl.GetString()))
            return await req.BadRequestAsync("'departureAirport' is required.");

        if (!body.TryGetProperty("ticketNumbers", out var ticketsEl) || ticketsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'ticketNumbers' array is required.");

        var departureAirport = airportEl.GetString()!.ToUpperInvariant().Trim();
        var ticketNumbers = ticketsEl.EnumerateArray()
            .Select(t => t.GetString())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();

        if (ticketNumbers.Count == 0)
            return await req.BadRequestAsync("At least one ticket number is required.");

        try
        {
            var result = await _deliveryServiceClient.GetBoardingDocsAsync(departureAirport, ticketNumbers, ct);

            return await req.OkJsonAsync(new
            {
                boardingCards = result.BoardingCards.Select(c => new
                {
                    ticketNumber = c.TicketNumber,
                    passengerId = c.PassengerId,
                    givenName = c.GivenName,
                    surname = c.Surname,
                    flightNumber = c.FlightNumber,
                    departureDate = c.DepartureDate,
                    seatNumber = c.SeatNumber,
                    cabinCode = c.CabinCode,
                    sequenceNumber = c.SequenceNumber,
                    origin = c.Origin,
                    destination = c.Destination,
                    bcbpString = c.BcbpString
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCI boarding-docs failed for departure airport {DepartureAirport}", departureAirport);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Shared guard — verifies all passengers on an order have travel documents.
    // Returns an error message if validation fails, null if all documents present.
    // -------------------------------------------------------------------------

    private async Task<string?> GetTravelDocumentErrorAsync(string bookingReference, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(bookingReference, ct);
        if (order is null)
            return "Booking not found.";

        if (order.OrderData is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return "Passenger travel documents have not been submitted.";

        if (!el.TryGetProperty("dataLists", out var dl) ||
            !dl.TryGetProperty("passengers", out var paxArr) ||
            paxArr.ValueKind != JsonValueKind.Array)
            return "Passenger travel documents have not been submitted.";

        foreach (var pax in paxArr.EnumerateArray())
        {
            var hasDoc = pax.TryGetProperty("docs", out var td) &&
                         td.ValueKind == JsonValueKind.Array &&
                         td.GetArrayLength() > 0 &&
                         td[0].TryGetProperty("number", out var num) &&
                         !string.IsNullOrWhiteSpace(num.GetString());
            if (!hasDoc)
                return "Passenger travel documents have not been submitted.";
        }

        return null;
    }
}
