using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Application.DeleteManifest;
using ReservationSystem.Microservices.Delivery.Application.GetManifest;
using ReservationSystem.Microservices.Delivery.Application.PatchManifest;
using ReservationSystem.Microservices.Delivery.Application.UpdateFlightTimes;
using ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;
using System.Web;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class ManifestFunction
{
    private readonly CreateManifestHandler _createHandler;
    private readonly UpdateManifestSeatHandler _updateSeatHandler;
    private readonly PatchManifestHandler _patchHandler;
    private readonly UpdateFlightTimesHandler _flightTimesHandler;
    private readonly DeleteManifestHandler _deleteHandler;
    private readonly GetManifestHandler _getHandler;
    private readonly ILogger<ManifestFunction> _logger;

    public ManifestFunction(
        CreateManifestHandler createHandler,
        UpdateManifestSeatHandler updateSeatHandler,
        PatchManifestHandler patchHandler,
        UpdateFlightTimesHandler flightTimesHandler,
        DeleteManifestHandler deleteHandler,
        GetManifestHandler getHandler,
        ILogger<ManifestFunction> logger)
    {
        _createHandler = createHandler;
        _updateSeatHandler = updateSeatHandler;
        _patchHandler = patchHandler;
        _flightTimesHandler = flightTimesHandler;
        _deleteHandler = deleteHandler;
        _getHandler = getHandler;
        _logger = logger;
    }

    // POST /v1/manifest
    [Function("CreateManifest")]
    [OpenApiOperation(operationId: "CreateManifest", tags: new[] { "Manifest" }, Summary = "Create passenger manifest entries for a booking")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateManifestRequest), Required = true, Description = "Manifest creation request: bookingReference, entries")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateManifestResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateManifestRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateManifestRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateManifest request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        if (request.Entries.Count == 0)
            return await req.BadRequestAsync("At least one manifest entry is required.");

        try
        {
            var result = await _createHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/manifest", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create manifest entries");
            return await req.InternalServerErrorAsync();
        }
    }

    // PUT /v1/manifest
    [Function("UpdateManifestSeat")]
    [OpenApiOperation(operationId: "UpdateManifestSeat", tags: new[] { "Manifest" }, Summary = "Update seat assignments in the manifest")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateManifestSeatRequest), Required = true, Description = "Seat update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateCountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> UpdateManifestSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        UpdateManifestSeatRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateManifestSeatRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateManifestSeat request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        try
        {
            var updated = await _updateSeatHandler.HandleAsync(request, cancellationToken);
            return await req.OkJsonAsync(new UpdateCountResponse { Updated = updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update manifest seats");
            return await req.InternalServerErrorAsync();
        }
    }

    // PATCH /v1/manifest/{bookingRef}
    [Function("PatchManifest")]
    [OpenApiOperation(operationId: "PatchManifest", tags: new[] { "Manifest" }, Summary = "Patch manifest entries for a booking")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PatchManifestRequest), Required = true, Description = "Patch request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateCountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> PatchManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifest/{bookingRef}")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        PatchManifestRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PatchManifestRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in PatchManifest request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        try
        {
            var updated = await _patchHandler.HandleAsync(bookingRef, request, cancellationToken);
            return await req.OkJsonAsync(new UpdateCountResponse { Updated = updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch manifest for {BookingRef}", bookingRef);
            return await req.InternalServerErrorAsync();
        }
    }

    // PATCH /v1/manifest/{bookingRef}/flight
    [Function("UpdateFlightTimes")]
    [OpenApiOperation(operationId: "UpdateFlightTimes", tags: new[] { "Manifest" }, Summary = "Update flight times in the manifest")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateFlightTimesRequest), Required = true, Description = "Flight times update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateCountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateFlightTimes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifest/{bookingRef}/flight")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        UpdateFlightTimesRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateFlightTimesRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateFlightTimes request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        try
        {
            var updated = await _flightTimesHandler.HandleAsync(bookingRef, request, cancellationToken);
            if (updated == 0)
                return await req.NotFoundAsync($"No manifest entries found for booking '{bookingRef}' on flight {request.FlightNumber}.");
            return await req.OkJsonAsync(new UpdateCountResponse { Updated = updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update flight times for {BookingRef}", bookingRef);
            return await req.InternalServerErrorAsync();
        }
    }

    // DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}
    [Function("DeleteManifest")]
    [OpenApiOperation(operationId: "DeleteManifest", tags: new[] { "Manifest" }, Summary = "Delete manifest entries for a flight on a booking")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Booking reference")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Flight number")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Departure date (yyyy-MM-dd)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateCountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}")] HttpRequestData req,
        string bookingRef,
        string flightNumber,
        string departureDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var date = DateTime.Parse(departureDate);
            var deleted = await _deleteHandler.HandleAsync(bookingRef, flightNumber, date, cancellationToken);

            if (deleted == 0)
                return await req.NotFoundAsync($"No manifest entries found for {bookingRef}/{flightNumber}/{departureDate}.");

            return await req.OkJsonAsync(new UpdateCountResponse { Deleted = deleted });
        }
        catch (FormatException)
        {
            return await req.BadRequestAsync("Invalid departure date format. Use yyyy-MM-dd.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete manifest for {BookingRef}/{FlightNumber}/{Date}",
                bookingRef, flightNumber, departureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/manifest?flightNumber={fn}&departureDate={date}
    [Function("GetManifest")]
    [OpenApiOperation(operationId: "GetManifest", tags: new[] { "Manifest" }, Summary = "Get the passenger manifest for a flight")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Flight number")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Departure date (yyyy-MM-dd)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetManifestResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var flightNumber = query["flightNumber"];
        var departureDateStr = query["departureDate"];

        if (string.IsNullOrWhiteSpace(flightNumber) || string.IsNullOrWhiteSpace(departureDateStr))
            return await req.BadRequestAsync("Query parameters 'flightNumber' and 'departureDate' are required.");

        try
        {
            var departureDate = DateTime.Parse(departureDateStr);
            var result = await _getHandler.HandleAsync(flightNumber, departureDate, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"No manifest entries found for {flightNumber} on {departureDateStr}.");

            return await req.OkJsonAsync(result);
        }
        catch (FormatException)
        {
            return await req.BadRequestAsync("Invalid departure date format. Use yyyy-MM-dd.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get manifest for {FlightNumber}/{Date}", flightNumber, departureDateStr);
            return await req.InternalServerErrorAsync();
        }
    }
}
