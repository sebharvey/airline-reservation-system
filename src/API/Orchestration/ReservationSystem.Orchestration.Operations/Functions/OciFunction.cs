using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.OciPax;
using ReservationSystem.Orchestration.Operations.Application.OciRetrieve;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for the Online Check-In (OCI) journey.
/// Orchestrates calls across Order MS, Delivery MS, and Customer MS.
/// </summary>
public sealed class OciFunction
{
    private readonly OciRetrieveHandler _retrieveHandler;
    private readonly OciPaxHandler _paxHandler;
    private readonly ILogger<OciFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OciFunction(
        OciRetrieveHandler retrieveHandler,
        OciPaxHandler paxHandler,
        ILogger<OciFunction> logger)
    {
        _retrieveHandler = retrieveHandler;
        _paxHandler = paxHandler;
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
        Summary = "Submit passport and travel document details for each PAX; completes check-in on final submission")]
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

            if (!p.TryGetProperty("travelDocument", out var tdEl) || tdEl.ValueKind != JsonValueKind.Object)
                continue;

            var type = tdEl.TryGetProperty("type", out var tyEl) ? tyEl.GetString() ?? "" : "";
            var number = tdEl.TryGetProperty("number", out var numEl) ? numEl.GetString() ?? "" : "";
            var issuingCountry = tdEl.TryGetProperty("issuingCountry", out var icEl) ? icEl.GetString() ?? "" : "";
            var nationality = tdEl.TryGetProperty("nationality", out var natEl) ? natEl.GetString() ?? "" : "";
            var issueDate = tdEl.TryGetProperty("issueDate", out var idEl) ? idEl.GetString() ?? "" : "";
            var expiryDate = tdEl.TryGetProperty("expiryDate", out var edEl) ? edEl.GetString() ?? "" : "";

            // Validate passport dates
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
                return await req.InternalServerErrorAsync();

            return await req.OkJsonAsync(new
            {
                bookingReference = result.BookingReference,
                success = result.Success,
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

        // Seat selection is not implemented at this time — return success
        return await req.OkJsonAsync(new { bookingReference = bookingReference.ToUpperInvariant().Trim(), success = true });
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

        // Baggage selection is not implemented at this time — return success
        return await req.OkJsonAsync(new { bookingReference = bookingReference.ToUpperInvariant().Trim(), success = true });
    }
}
