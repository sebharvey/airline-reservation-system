using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// Staff-facing manifest endpoints: retrieve manifest and manage seat assignments at departure.
/// Requires a valid staff JWT (enforced by TerminalAuthenticationMiddleware).
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class AdminManifestFunction
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<AdminManifestFunction> _logger;

    public AdminManifestFunction(
        DeliveryServiceClient deliveryServiceClient,
        OrderServiceClient orderServiceClient,
        ILogger<AdminManifestFunction> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // GET /v1/admin/manifest?flightNumber=AX001&departureDate=yyyy-MM-dd
    [Function("AdminGetFlightManifest")]
    [OpenApiOperation(operationId: "AdminGetFlightManifest", tags: new[] { "Admin Manifest" },
        Summary = "Return the passenger manifest for a flight from delivery.Manifest (staff)")]
    [OpenApiParameter(name: "flightNumber",  In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Flight number, e.g. AX001")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Departure date (yyyy-MM-dd)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminFlightManifestResult), Description = "OK — manifest entries")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,   Description = "Not Found — no manifest for this flight")]
    public async Task<HttpResponseData> GetFlightManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var flightNumber     = qs["flightNumber"];
        var departureDateRaw = qs["departureDate"];

        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("flightNumber is required.");

        if (string.IsNullOrWhiteSpace(departureDateRaw) ||
            !DateOnly.TryParseExact(departureDateRaw, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("departureDate must be in yyyy-MM-dd format.");

        var result = await _deliveryServiceClient.GetManifestByFlightAsync(
            flightNumber, departureDateRaw, cancellationToken);

        if (result is null)
        {
            _logger.LogWarning("Manifest retrieval failed for {FlightNumber} {DepartureDate}", flightNumber, departureDateRaw);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        return await req.OkJsonAsync(result);
    }

    // POST /v1/admin/manifest/release-seat
    [Function("AdminReleaseSeat")]
    [OpenApiOperation(operationId: "AdminReleaseSeat", tags: new[] { "Admin Manifest" },
        Summary = "Release a passenger's seat assignment from the manifest and order (staff)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent,  Description = "No Content — seat released")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,  Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,    Description = "Not Found — manifest entry not found")]
    public async Task<HttpResponseData> ReleaseSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/manifest/release-seat")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        AdminSeatRequest? body;
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(req.Body, cancellationToken: cancellationToken);
            body = System.Text.Json.JsonSerializer.Deserialize<AdminSeatRequest>(doc.RootElement.GetRawText());
        }
        catch { return await req.BadRequestAsync("Request body must be valid JSON."); }

        if (body is null || string.IsNullOrWhiteSpace(body.ETicketNumber) || string.IsNullOrWhiteSpace(body.BookingReference))
            return await req.BadRequestAsync("eTicketNumber and bookingReference are required.");

        // 1. Clear seat from delivery.Manifest
        var manifestUpdated = await _deliveryServiceClient.UpdateManifestSeatAsync(
            body.ETicketNumber, null, cancellationToken);

        if (!manifestUpdated)
        {
            _logger.LogWarning("Release seat: manifest entry not found for e-ticket {ETicketNumber}", body.ETicketNumber);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // 2. Clear seat from order (best-effort — manifest is the authoritative departure record)
        if (!string.IsNullOrWhiteSpace(body.PassengerId) && !string.IsNullOrWhiteSpace(body.InventoryId))
        {
            try
            {
                var seatsPayload = new[]
                {
                    new { passengerId = body.PassengerId, segmentId = body.InventoryId, seatNumber = (string?)null, price = 0m, tax = 0m, currency = "GBP" }
                };
                await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(body.BookingReference, seatsPayload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Release seat: order seat update failed for booking {BookingRef} — manifest was cleared", body.BookingReference);
            }
        }

        _logger.LogInformation("Seat released for e-ticket {ETicketNumber} on booking {BookingRef}", body.ETicketNumber, body.BookingReference);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    // POST /v1/admin/manifest/assign-seat
    [Function("AdminAssignSeat")]
    [OpenApiOperation(operationId: "AdminAssignSeat", tags: new[] { "Admin Manifest" },
        Summary = "Assign a seat to a passenger on the manifest and order (staff)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent,  Description = "No Content — seat assigned")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,  Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,    Description = "Not Found — manifest entry not found")]
    public async Task<HttpResponseData> AssignSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/manifest/assign-seat")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        AdminSeatRequest? body;
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(req.Body, cancellationToken: cancellationToken);
            body = System.Text.Json.JsonSerializer.Deserialize<AdminSeatRequest>(doc.RootElement.GetRawText());
        }
        catch { return await req.BadRequestAsync("Request body must be valid JSON."); }

        if (body is null || string.IsNullOrWhiteSpace(body.ETicketNumber) ||
            string.IsNullOrWhiteSpace(body.BookingReference) || string.IsNullOrWhiteSpace(body.SeatNumber))
            return await req.BadRequestAsync("eTicketNumber, bookingReference, and seatNumber are required.");

        // 1. Update seat on delivery.Manifest
        var manifestUpdated = await _deliveryServiceClient.UpdateManifestSeatAsync(
            body.ETicketNumber, body.SeatNumber, cancellationToken);

        if (!manifestUpdated)
        {
            _logger.LogWarning("Assign seat: manifest entry not found for e-ticket {ETicketNumber}", body.ETicketNumber);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // 2. Update seat on order (best-effort — manifest is the authoritative departure record)
        if (!string.IsNullOrWhiteSpace(body.PassengerId) && !string.IsNullOrWhiteSpace(body.InventoryId))
        {
            try
            {
                var seatsPayload = new[]
                {
                    new { passengerId = body.PassengerId, segmentId = body.InventoryId, seatNumber = body.SeatNumber, price = 0m, tax = 0m, currency = "GBP" }
                };
                await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(body.BookingReference, seatsPayload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Assign seat: order seat update failed for booking {BookingRef} — manifest was updated", body.BookingReference);
            }
        }

        _logger.LogInformation("Seat {SeatNumber} assigned for e-ticket {ETicketNumber} on booking {BookingRef}",
            body.SeatNumber, body.ETicketNumber, body.BookingReference);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

file sealed class AdminSeatRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("passengerId")]
    public string? PassengerId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("inventoryId")]
    public string? InventoryId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }
}
