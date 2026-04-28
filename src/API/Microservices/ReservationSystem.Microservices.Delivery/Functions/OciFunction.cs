using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.OciBoardingDocs;
using ReservationSystem.Microservices.Delivery.Application.OciCheckIn;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Functions;

/// <summary>
/// HTTP-triggered functions for Online Check-In (OCI) document processing.
/// </summary>
public sealed class OciFunction
{
    private readonly OciCheckInHandler _checkInHandler;
    private readonly OciBoardingDocsHandler _boardingDocsHandler;
    private readonly ILogger<OciFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OciFunction(
        OciCheckInHandler checkInHandler,
        OciBoardingDocsHandler boardingDocsHandler,
        ILogger<OciFunction> logger)
    {
        _checkInHandler = checkInHandler;
        _boardingDocsHandler = boardingDocsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/checkin
    // -------------------------------------------------------------------------

    [Function("OciCheckIn")]
    [OpenApiOperation(operationId: "OciCheckIn", tags: new[] { "OCI" }, Summary = "Check in tickets for a departure airport")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Check-in request with departureAirport and tickets array")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Timatic document or APIS check failed")]
    public async Task<HttpResponseData> OciCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/checkin")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, cancellationToken); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("departureAirport", out var airportEl) || string.IsNullOrWhiteSpace(airportEl.GetString()))
            return await req.BadRequestAsync("'departureAirport' is required.");

        if (!body.TryGetProperty("tickets", out var ticketsEl) || ticketsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'tickets' array is required.");

        var departureAirport = airportEl.GetString()!.ToUpperInvariant().Trim();
        var bypassTimatic = body.TryGetProperty("bypassTimatic", out var btEl) && btEl.ValueKind == JsonValueKind.True;

        var tickets = new List<OciCheckInTicket>();
        foreach (var t in ticketsEl.EnumerateArray())
        {
            var ticketNumber = t.TryGetProperty("ticketNumber", out var tn) ? tn.GetString() : null;
            if (string.IsNullOrWhiteSpace(ticketNumber)) continue;
            var passengerId = t.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";
            var givenName = t.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "";
            var surname = t.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "";

            // Optional travel document fields forwarded from the Operations API
            var docNationality = t.TryGetProperty("docNationality", out var nat) ? nat.GetString() : null;
            var docNumber = t.TryGetProperty("docNumber", out var num) ? num.GetString() : null;
            var docIssuingCountry = t.TryGetProperty("docIssuingCountry", out var ic) ? ic.GetString() : null;
            var docExpiryDate = t.TryGetProperty("docExpiryDate", out var ed) ? ed.GetString() : null;

            tickets.Add(new OciCheckInTicket(
                ticketNumber, passengerId, givenName, surname,
                docNationality, docNumber, docIssuingCountry, docExpiryDate));
        }

        if (tickets.Count == 0)
            return await req.BadRequestAsync("At least one valid ticket entry is required.");

        try
        {
            var command = new OciCheckInCommand(departureAirport, tickets, bypassTimatic);
            var result = await _checkInHandler.HandleAsync(command, cancellationToken);

            return await req.OkJsonAsync(new
            {
                checkedIn = result.CheckedIn,
                tickets = result.Tickets.Select(t => new { ticketNumber = t.TicketNumber, status = t.Status, seatNumber = t.SeatNumber }),
                timaticNotes = result.TimaticNotes.Select(n => new
                {
                    checkType    = n.CheckType,
                    ticketNumber = n.TicketNumber,
                    status       = n.Status,
                    detail       = n.Detail,
                    timestamp    = n.Timestamp
                })
            });
        }
        catch (TimaticValidationException ex)
        {
            _logger.LogWarning("OCI check-in blocked by Timatic for {DepartureAirport}: {Message}", departureAirport, ex.Message);

            // Return structured 422 so the Operations API can record the timatic notes on the order
            var response = req.CreateResponse(System.Net.HttpStatusCode.UnprocessableEntity);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = ex.Message,
                timaticNotes = ex.TimaticNotes.Select(n => new
                {
                    checkType    = n.CheckType,
                    ticketNumber = n.TicketNumber,
                    status       = n.Status,
                    detail       = n.Detail,
                    timestamp    = n.Timestamp
                })
            }, JsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCI check-in failed for departure airport {DepartureAirport}", departureAirport);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/oci/boarding-docs
    // -------------------------------------------------------------------------

    [Function("OciBoardingDocs")]
    [OpenApiOperation(operationId: "OciBoardingDocs", tags: new[] { "OCI" }, Summary = "Generate boarding documents for checked-in tickets")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Boarding docs request with departureAirport and ticketNumbers array")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> OciBoardingDocs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/oci/boarding-docs")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, cancellationToken); }
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
            var command = new OciBoardingDocsCommand(departureAirport, ticketNumbers);
            var result = await _boardingDocsHandler.HandleAsync(command, cancellationToken);

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
}
