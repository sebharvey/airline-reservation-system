using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.RebookManifest;
using ReservationSystem.Microservices.Delivery.Application.WriteManifest;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class ManifestFunction
{
    private readonly WriteManifestHandler _writeHandler;
    private readonly RebookManifestHandler _rebookHandler;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<ManifestFunction> _logger;

    public ManifestFunction(
        WriteManifestHandler writeHandler,
        RebookManifestHandler rebookHandler,
        IManifestRepository manifestRepository,
        ILogger<ManifestFunction> logger)
    {
        _writeHandler = writeHandler;
        _rebookHandler = rebookHandler;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    // GET /v1/manifest?flightNumber={}&departureDate={}
    [Function("GetManifest")]
    [OpenApiOperation(operationId: "GetManifest", tags: new[] { "Manifest" }, Summary = "Retrieve the full passenger manifest for a flight")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — passenger manifest entries for the flight")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no manifest entries for this flight")]
    public async Task<HttpResponseData> GetManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var flightNumber = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["flightNumber"];
        var departureDateRaw = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["departureDate"];

        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("flightNumber is required.");

        if (!DateOnly.TryParseExact(departureDateRaw, "yyyy-MM-dd", out var departureDate))
            return await req.BadRequestAsync("departureDate must be in yyyy-MM-dd format.");

        var entries = await _manifestRepository.GetByFlightAsync(flightNumber, departureDate, cancellationToken);

        if (entries.Count == 0)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new
        {
            entries = entries.Select(e => new
            {
                orderId          = e.OrderId,
                bookingReference = e.BookingReference,
                passengerId      = e.PassengerId,
                givenName        = e.GivenName,
                surname          = e.Surname,
                eTicketNumber    = e.ETicketNumber,
                seatNumber       = string.IsNullOrEmpty(e.SeatNumber) ? (string?)null : e.SeatNumber,
                cabinCode        = e.CabinCode,
                bookingType      = e.BookingType,
                checkedIn        = e.CheckedIn,
                checkedInAt      = e.CheckedInAt,
                ssrCodes         = string.IsNullOrEmpty(e.SsrCodes)
                    ? []
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(e.SsrCodes) ?? [],
                gender           = e.Gender,
                dateOfBirth      = e.DateOfBirth,
                ptcCode          = e.PtcCode
            })
        });
    }

    // POST /v1/manifest
    [Function("WriteManifest")]
    [OpenApiOperation(operationId: "WriteManifest", tags: new[] { "Manifest" }, Summary = "Write passenger manifest entries for a flight segment")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(WriteManifestRequest), Required = true, Description = "Manifest write request: booking reference, flight details, and one entry per passenger")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(WriteManifestResponse), Description = "Created — returns written and skipped counts")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> WriteManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<WriteManifestRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("bookingReference is required.");

        if (request.Entries.Count == 0)
            return await req.BadRequestAsync("entries must contain at least one entry.");

        try
        {
            var result = await _writeHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/manifest", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write manifest for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // PATCH /v1/manifest/{eTicketNumber}/seat
    [Function("UpdateManifestSeat")]
    [OpenApiOperation(operationId: "UpdateManifestSeat", tags: new[] { "Manifest" }, Summary = "Update the seat number on a manifest entry by e-ticket number")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content — seat updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,  Description = "Not Found — no manifest entry for this e-ticket")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> UpdateManifestSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifest/{eTicketNumber}/seat")] HttpRequestData req,
        string eTicketNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eTicketNumber))
            return await req.BadRequestAsync("eTicketNumber is required.");

        string? newSeatNumber = null;
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(req.Body, cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("seatNumber", out var seatEl))
                newSeatNumber = seatEl.ValueKind == System.Text.Json.JsonValueKind.Null ? null : seatEl.GetString();
        }
        catch
        {
            return await req.BadRequestAsync("Request body must be valid JSON.");
        }

        var updated = await _manifestRepository.UpdateSeatByETicketAsync(eTicketNumber, newSeatNumber, cancellationToken);

        return updated
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }

    // DELETE /v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}
    [Function("DeleteManifestFlight")]
    [OpenApiOperation(operationId: "DeleteManifestFlight", tags: new[] { "Manifest" }, Summary = "Remove manifest entries for a booking on a specific cancelled flight")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK — entries removed")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no manifest entries matched")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> DeleteManifestFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}")] HttpRequestData req,
        string bookingReference,
        string flightNumber,
        string departureDate,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", out var departureDateOnly))
            return await req.BadRequestAsync("departureDate must be in yyyy-MM-dd format.");

        var deleted = await _manifestRepository.DeleteByBookingAndFlightAsync(
            bookingReference, flightNumber, departureDateOnly, cancellationToken);

        if (deleted == 0)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return req.CreateResponse(HttpStatusCode.OK);
    }

    // PATCH /v1/manifest/{bookingReference}
    [Function("UpdateManifestSsrs")]
    [OpenApiOperation(operationId: "UpdateManifestSsrs", tags: new[] { "Manifest" }, Summary = "Replace SSR codes on manifest entries for a booking; accepts one entry per e-ticket number; an empty ssrCodes array clears all SSRs for that entry")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — returns count of updated entries")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no manifest entries matched")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> UpdateManifestSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifest/{bookingReference}")] HttpRequestData req,
        string bookingReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookingReference))
            return await req.BadRequestAsync("bookingReference is required.");

        var (request, error) = await req.TryDeserializeBodyAsync<UpdateManifestSsrsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.Entries.Count == 0)
            return await req.BadRequestAsync("entries must contain at least one entry.");

        var ssrsByETicket = request.Entries.ToDictionary(
            e => e.ETicketNumber,
            e => e.SsrCodes.Count > 0 ? JsonSerializer.Serialize(e.SsrCodes) : (string?)null);

        var updated = await _manifestRepository.UpdateSsrCodesByBookingAsync(
            bookingReference, ssrsByETicket, cancellationToken);

        if (updated == 0)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new { updated });
    }

    // PATCH /v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}
    [Function("RebookManifestFlight")]
    [OpenApiOperation(operationId: "RebookManifestFlight", tags: new[] { "Manifest" }, Summary = "Update manifest entries in place for a rebooked flight segment")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — returns count of updated entries")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no manifest entries matched")]
    public async Task<HttpResponseData> RebookManifestFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}")] HttpRequestData req,
        string bookingReference,
        string flightNumber,
        string departureDate,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", out var fromDepartureDate))
            return await req.BadRequestAsync("departureDate must be in yyyy-MM-dd format.");

        var (request, error) = await req.TryDeserializeBodyAsync<RebookManifestFlightRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.ToInventoryId == Guid.Empty)
            return await req.BadRequestAsync("toInventoryId is required.");

        if (string.IsNullOrWhiteSpace(request.ToFlightNumber))
            return await req.BadRequestAsync("toFlightNumber is required.");

        if (!DateOnly.TryParseExact(request.ToDepartureDate, "yyyy-MM-dd", out var toDepartureDate))
            return await req.BadRequestAsync("toDepartureDate must be in yyyy-MM-dd format.");

        if (!TimeOnly.TryParseExact(request.ToDepartureTime, "HH:mm", out var toDepartureTime))
            return await req.BadRequestAsync("toDepartureTime must be in HH:mm format.");

        if (!TimeOnly.TryParseExact(request.ToArrivalTime, "HH:mm", out var toArrivalTime))
            return await req.BadRequestAsync("toArrivalTime must be in HH:mm format.");

        if (request.Passengers.Count == 0)
            return await req.BadRequestAsync("passengers must contain at least one entry.");

        var command = new RebookManifestCommand(
            BookingReference:  bookingReference,
            FromFlightNumber:  flightNumber,
            FromDepartureDate: fromDepartureDate,
            ToInventoryId:     request.ToInventoryId,
            ToFlightNumber:    request.ToFlightNumber,
            ToOrigin:          request.ToOrigin,
            ToDestination:     request.ToDestination,
            ToDepartureDate:   toDepartureDate,
            ToDepartureTime:   toDepartureTime,
            ToArrivalTime:     toArrivalTime,
            ToCabinCode:       request.ToCabinCode,
            Passengers:        request.Passengers.Select(p => (p.PassengerId, p.ETicketNumber)).ToList());

        var updated = await _rebookHandler.HandleAsync(command, cancellationToken);

        if (updated == 0)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new { updated });
    }
}
