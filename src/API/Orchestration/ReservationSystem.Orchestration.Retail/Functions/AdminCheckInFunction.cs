using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Retail.Application.AdminCheckIn;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// Staff-facing check-in endpoint. The "Admin" function name prefix activates
/// <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>,
/// requiring a valid staff JWT for all calls.
/// </summary>
public sealed class AdminCheckInFunction
{
    private readonly AdminCheckInHandler _handler;
    private readonly ILogger<AdminCheckInFunction> _logger;

    public AdminCheckInFunction(AdminCheckInHandler handler, ILogger<AdminCheckInFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/checkin/{bookingRef}
    // -------------------------------------------------------------------------

    [Function("AdminCheckIn")]
    [OpenApiOperation(operationId: "AdminCheckIn", tags: new[] { "Admin CheckIn" },
        Summary = "Complete check-in for all passengers on a booking (staff)")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The 6-character booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ departureAirport, passengers: [{ ticketNumber, travelDocument: { type, number, issuingCountry, nationality, issueDate, expiryDate } }] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object),
        Description = "{ bookingReference, boardingCards: [...] }")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Booking not found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/checkin/{bookingRef}")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, cancellationToken: cancellationToken); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (string.IsNullOrWhiteSpace(bookingRef))
            return await req.BadRequestAsync("'bookingRef' path parameter is required.");

        var departureAirport = body.TryGetProperty("departureAirport", out var daEl) ? daEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(departureAirport))
            return await req.BadRequestAsync("'departureAirport' is required.");

        if (!body.TryGetProperty("passengers", out var passengersEl) || passengersEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'passengers' array is required.");

        var passengers = new List<AdminCheckInPassenger>();
        foreach (var p in passengersEl.EnumerateArray())
        {
            var ticketNumber = p.TryGetProperty("ticketNumber", out var tnEl) ? tnEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(ticketNumber))
                return await req.BadRequestAsync("Each passenger must have a 'ticketNumber'.");

            if (!p.TryGetProperty("travelDocument", out var docEl) || docEl.ValueKind != JsonValueKind.Object)
                return await req.BadRequestAsync($"Passenger '{ticketNumber}' is missing 'travelDocument'.");

            var type = docEl.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "PASSPORT" : "PASSPORT";
            var number = docEl.TryGetProperty("number", out var numEl) ? numEl.GetString() ?? "" : "";
            var issuingCountry = docEl.TryGetProperty("issuingCountry", out var icEl) ? icEl.GetString() ?? "" : "";
            var nationality = docEl.TryGetProperty("nationality", out var natEl) ? natEl.GetString() ?? "" : "";
            var issueDate = docEl.TryGetProperty("issueDate", out var isEl) ? isEl.GetString() ?? "" : "";
            var expiryDate = docEl.TryGetProperty("expiryDate", out var exEl) ? exEl.GetString() ?? "" : "";

            passengers.Add(new AdminCheckInPassenger(
                ticketNumber,
                new AdminCheckInTravelDocument(type, number, issuingCountry, nationality, issueDate, expiryDate)));
        }

        if (passengers.Count == 0)
            return await req.BadRequestAsync("At least one passenger is required.");

        try
        {
            var command = new AdminCheckInCommand(
                bookingRef.ToUpperInvariant().Trim(),
                departureAirport.ToUpperInvariant().Trim(),
                passengers);

            var result = await _handler.HandleAsync(command, cancellationToken);
            if (result is null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            return await req.OkJsonAsync(new
            {
                bookingReference = result.BookingReference,
                timaticNotes = result.TimaticNotes.Select(n => new
                {
                    checkType = n.CheckType,
                    ticketNumber = n.TicketNumber,
                    status = n.Status,
                    detail = n.Detail,
                }),
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
                    bcbpString = c.BcbpString,
                })
            });
        }
        catch (AdminOciTimaticBlockedException ex)
        {
            _logger.LogWarning("Admin check-in blocked by Timatic for {BookingRef}: {Message}", bookingRef, ex.Message);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Admin check-in failed for {BookingRef}: {Message}", bookingRef, ex.Message);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin check-in unexpected error for {BookingRef}", bookingRef);
            return await req.InternalServerErrorAsync();
        }
    }
}
