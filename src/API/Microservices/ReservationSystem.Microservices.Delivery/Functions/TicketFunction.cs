using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.IssueTickets;
using ReservationSystem.Microservices.Delivery.Application.VoidTicket;
using ReservationSystem.Microservices.Delivery.Application.ReissueTickets;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class TicketFunction
{
    private readonly IssueTicketsHandler _issueHandler;
    private readonly VoidTicketHandler _voidHandler;
    private readonly ReissueTicketsHandler _reissueHandler;
    private readonly ILogger<TicketFunction> _logger;

    public TicketFunction(
        IssueTicketsHandler issueHandler,
        VoidTicketHandler voidHandler,
        ReissueTicketsHandler reissueHandler,
        ILogger<TicketFunction> logger)
    {
        _issueHandler = issueHandler;
        _voidHandler = voidHandler;
        _reissueHandler = reissueHandler;
        _logger = logger;
    }

    // POST /v1/tickets
    [Function("IssueTickets")]
    public async Task<HttpResponseData> IssueTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tickets")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        IssueTicketsRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<IssueTicketsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in IssueTickets request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        if (request.Passengers.Count == 0)
            return await req.BadRequestAsync("At least one passenger is required.");

        if (request.Segments.Count == 0)
            return await req.BadRequestAsync("At least one segment is required.");

        try
        {
            var result = await _issueHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/tickets", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue tickets for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // PATCH /v1/tickets/{eTicketNumber}/void
    [Function("VoidTicket")]
    public async Task<HttpResponseData> VoidTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/tickets/{eTicketNumber}/void")] HttpRequestData req,
        string eTicketNumber,
        CancellationToken cancellationToken)
    {
        VoidTicketRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<VoidTicketRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in VoidTicket request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return await req.BadRequestAsync("The 'reason' field is required.");

        try
        {
            var result = await _voidHandler.HandleAsync(eTicketNumber, request, cancellationToken);
            if (result is null)
                return await req.NotFoundAsync($"Ticket '{eTicketNumber}' not found or already voided.");
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to void ticket {ETicketNumber}", eTicketNumber);
            return await req.InternalServerErrorAsync();
        }
    }

    // POST /v1/tickets/reissue
    [Function("ReissueTickets")]
    public async Task<HttpResponseData> ReissueTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tickets/reissue")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        ReissueTicketsRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ReissueTicketsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ReissueTickets request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
}
