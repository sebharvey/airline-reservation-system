using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Functions;

/// <summary>
/// HTTP-triggered functions for managing passenger departure manifests.
/// </summary>
public sealed class ManifestFunction
{
    private readonly CreateManifestHandler _createHandler;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<ManifestFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ManifestFunction(
        CreateManifestHandler createHandler,
        IManifestRepository manifestRepository,
        ILogger<ManifestFunction> logger)
    {
        _createHandler = createHandler;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/manifest
    // -------------------------------------------------------------------------

    [Function("CreateManifest")]
    [OpenApiOperation(operationId: "CreateManifest", tags: new[] { "Manifest" }, Summary = "Create manifest entries for a booking")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ bookingReference, entries: [{ ticketId, inventoryId, flightNumber, origin, destination, departureDate, eTicketNumber, passengerId, givenName, surname, cabinCode, seatNumber? }] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(object), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, cancellationToken); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("bookingReference", out var brEl) || string.IsNullOrWhiteSpace(brEl.GetString()))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (!body.TryGetProperty("entries", out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'entries' array is required.");

        var bookingReference = brEl.GetString()!.ToUpperInvariant().Trim();
        var entries = new List<ManifestEntryRequest>();

        foreach (var e in entriesEl.EnumerateArray())
        {
            var ticketId = e.TryGetProperty("ticketId", out var tidEl) ? tidEl.GetString() ?? "" : "";
            var inventoryId = e.TryGetProperty("inventoryId", out var iidEl) ? iidEl.GetString() ?? "" : "";
            var flightNumber = e.TryGetProperty("flightNumber", out var fnEl) ? fnEl.GetString() ?? "" : "";
            var origin = e.TryGetProperty("origin", out var origEl) ? origEl.GetString() ?? "" : "";
            var destination = e.TryGetProperty("destination", out var destEl) ? destEl.GetString() ?? "" : "";
            var departureDate = e.TryGetProperty("departureDate", out var ddEl) ? ddEl.GetString() ?? "" : "";
            var eTicketNumber = e.TryGetProperty("eTicketNumber", out var etnEl) ? etnEl.GetString() ?? "" : "";
            var passengerId = e.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() ?? "" : "";
            var givenName = e.TryGetProperty("givenName", out var gnEl) ? gnEl.GetString() ?? "" : "";
            var surname = e.TryGetProperty("surname", out var snEl) ? snEl.GetString() ?? "" : "";
            var cabinCode = e.TryGetProperty("cabinCode", out var ccEl) ? ccEl.GetString() ?? "" : "";
            var seatNumber = e.TryGetProperty("seatNumber", out var seEl) ? seEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(eTicketNumber)) continue;

            entries.Add(new ManifestEntryRequest(
                ticketId, inventoryId, flightNumber, origin, destination,
                departureDate, eTicketNumber, passengerId, givenName, surname, cabinCode, seatNumber));
        }

        if (entries.Count == 0)
            return await req.BadRequestAsync("No valid entries provided.");

        try
        {
            var command = new CreateManifestCommand(bookingReference, entries);
            await _createHandler.HandleAsync(command, cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { bookingReference, created = entries.Count }, JsonOptions, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manifest creation failed for {BookingReference}", bookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}
    // -------------------------------------------------------------------------

    [Function("DeleteManifest")]
    [OpenApiOperation(operationId: "DeleteManifest", tags: new[] { "Manifest" }, Summary = "Delete manifest entries for a booking/flight")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> DeleteManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}")] HttpRequestData req,
        string bookingReference,
        string flightNumber,
        string departureDate,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(departureDate, out var depDate))
            return await req.BadRequestAsync($"Invalid departure date '{departureDate}'.");

        try
        {
            var deleted = await _manifestRepository.DeleteByBookingAndFlightAsync(
                bookingReference.ToUpperInvariant().Trim(),
                flightNumber,
                depDate,
                cancellationToken);

            return await req.OkJsonAsync(new { bookingReference, flightNumber, departureDate, deleted });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manifest deletion failed for {BookingReference}/{FlightNumber}/{Date}",
                bookingReference, flightNumber, departureDate);
            return await req.InternalServerErrorAsync();
        }
    }
}
