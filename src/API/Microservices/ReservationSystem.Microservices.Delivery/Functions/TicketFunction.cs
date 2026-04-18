using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Delivery.Application.GetCouponValue;
using ReservationSystem.Microservices.Delivery.Application.GetTicketsByBooking;
using ReservationSystem.Microservices.Delivery.Application.IssueTickets;
using ReservationSystem.Microservices.Delivery.Application.VoidTicket;
using ReservationSystem.Microservices.Delivery.Application.ReissueTickets;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using FluentValidation;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class TicketFunction
{
    private readonly GetTicketsByBookingHandler _getByBookingHandler;
    private readonly IssueTicketsHandler _issueHandler;
    private readonly VoidTicketHandler _voidHandler;
    private readonly ReissueTicketsHandler _reissueHandler;
    private readonly GetCouponValueHandler _couponValueHandler;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<TicketFunction> _logger;

    public TicketFunction(
        GetTicketsByBookingHandler getByBookingHandler,
        IssueTicketsHandler issueHandler,
        VoidTicketHandler voidHandler,
        ReissueTicketsHandler reissueHandler,
        GetCouponValueHandler couponValueHandler,
        ITicketRepository ticketRepository,
        ILogger<TicketFunction> logger)
    {
        _getByBookingHandler = getByBookingHandler;
        _issueHandler = issueHandler;
        _voidHandler = voidHandler;
        _reissueHandler = reissueHandler;
        _couponValueHandler = couponValueHandler;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    // GET /v1/tickets?bookingRef={bookingRef}
    [Function("GetTicketsByBooking")]
    [OpenApiOperation(operationId: "GetTicketsByBooking", tags: new[] { "Tickets" }, Summary = "Get all tickets for a booking reference")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The 6-character booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<GetTicketResponse>), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetTicketsByBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tickets")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var bookingRef = queryParams["bookingRef"]?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(bookingRef))
            return await req.BadRequestAsync("The 'bookingRef' query parameter is required.");

        var result = await _getByBookingHandler.HandleAsync(bookingRef, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // POST /v1/tickets
    [Function("IssueTickets")]
    [OpenApiOperation(operationId: "IssueTickets", tags: new[] { "Tickets" }, Summary = "Issue e-tickets — one per passenger covering all flight segments")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(IssueTicketsRequest), Required = true, Description = "Ticket issuance request: bookingReference, passengers (each with fareConstruction), segments")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(IssueTicketsResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> IssueTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tickets")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<IssueTicketsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _issueHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/tickets", result);
        }
        catch (ValidationException vex)
        {
            var messages = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
            return await req.BadRequestAsync(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue tickets for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value
    [Function("GetCouponValue")]
    [OpenApiOperation(operationId: "GetCouponValue", tags: new[] { "Tickets" }, Summary = "Get the derived attributed value for a single coupon")]
    [OpenApiParameter(name: "eTicketNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Full IATA e-ticket number, e.g. 932-1000000001")]
    [OpenApiParameter(name: "couponNumber", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "1-based coupon number (1–4)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetCouponValueResponse), Description = "Derived coupon value (fareShare, taxShare, total)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Ticket or coupon not found")]
    public async Task<HttpResponseData> GetCouponValue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value")] HttpRequestData req,
        string eTicketNumber,
        int couponNumber,
        CancellationToken cancellationToken)
    {
        if (couponNumber < 1 || couponNumber > 4)
            return await req.BadRequestAsync("couponNumber must be between 1 and 4.");

        var result = await _couponValueHandler.HandleAsync(eTicketNumber, couponNumber, cancellationToken);
        if (result is null)
            return await req.NotFoundAsync($"Ticket '{eTicketNumber}' or coupon {couponNumber} not found.");

        return await req.OkJsonAsync(result);
    }

    // PATCH /v1/tickets/{eTicketNumber}/void
    [Function("VoidTicket")]
    [OpenApiOperation(operationId: "VoidTicket", tags: new[] { "Tickets" }, Summary = "Void an e-ticket")]
    [OpenApiParameter(name: "eTicketNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "E-ticket number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VoidTicketRequest), Required = true, Description = "Void request: reason")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(VoidTicketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity")]
    public async Task<HttpResponseData> VoidTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/tickets/{eTicketNumber}/void")] HttpRequestData req,
        string eTicketNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<VoidTicketRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Reason))
            return await req.BadRequestAsync("The 'reason' field is required.");

        try
        {
            var result = await _voidHandler.HandleAsync(eTicketNumber, request, cancellationToken);
            if (result is null)
                return await req.NotFoundAsync($"Ticket '{eTicketNumber}' not found.");
            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            return await req.UnprocessableEntityAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to void ticket {ETicketNumber}", eTicketNumber);
            return await req.InternalServerErrorAsync();
        }
    }

    // POST /v1/tickets/reissue
    [Function("ReissueTickets")]
    [OpenApiOperation(operationId: "ReissueTickets", tags: new[] { "Tickets" }, Summary = "Reissue tickets (void old, issue new)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ReissueTicketsRequest), Required = true, Description = "Reissue request: voidedETicketNumbers, new ticket details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ReissueTicketsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> ReissueTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tickets/reissue")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ReissueTicketsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.VoidedETicketNumbers.Count == 0)
            return await req.BadRequestAsync("At least one e-ticket number to void is required.");

        try
        {
            var result = await _reissueHandler.HandleAsync(request, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reissue tickets");
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/debug/tickets?bookingRef={bookingRef}
    [Function("DebugGetTicketsByBooking")]
    [OpenApiOperation(operationId: "DebugGetTicketsByBooking", tags: new[] { "Debug" }, Summary = "[TEMP] Return raw Ticket database rows by booking reference")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object[]), Description = "Raw Ticket rows as JSON")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> DebugGetTicketsByBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/debug/tickets")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var bookingRef = queryParams["bookingRef"]?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(bookingRef))
            return await req.BadRequestAsync("The 'bookingRef' query parameter is required.");

        var tickets = await _ticketRepository.GetByBookingReferenceAsync(bookingRef, cancellationToken);

        var rows = tickets.Select(t =>
        {
            JsonElement ticketDataElement;
            try { ticketDataElement = JsonSerializer.Deserialize<JsonElement>(t.TicketData); }
            catch { ticketDataElement = JsonSerializer.Deserialize<JsonElement>("{}"); }

            return (object)new
            {
                ticketId = t.TicketId,
                ticketNumber = t.TicketNumber,
                bookingReference = t.BookingReference,
                passengerId = t.PassengerId,
                isVoided = t.IsVoided,
                voidedAt = t.VoidedAt,
                version = t.Version,
                createdAt = t.CreatedAt,
                updatedAt = t.UpdatedAt,
                ticketData = ticketDataElement
            };
        });

        return await req.OkJsonAsync(rows);
    }
}
