using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Admin.Application.GetPayment;
using ReservationSystem.Orchestration.Admin.Application.GetPaymentEvents;
using ReservationSystem.Orchestration.Admin.Application.GetPaymentsByDate;
using ReservationSystem.Orchestration.Admin.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Orchestration.Admin.Functions;

/// <summary>
/// HTTP-triggered functions for admin payment reporting (read-only).
/// All function names start with "Admin" so that TerminalAuthenticationMiddleware
/// validates the staff JWT token and role claim.
/// Orchestrates calls to the Payment microservice.
/// </summary>
public sealed class PaymentManagementFunction
{
    private readonly GetPaymentsByDateHandler _getPaymentsByDateHandler;
    private readonly GetPaymentHandler _getPaymentHandler;
    private readonly GetPaymentEventsHandler _getPaymentEventsHandler;
    private readonly ILogger<PaymentManagementFunction> _logger;

    public PaymentManagementFunction(
        GetPaymentsByDateHandler getPaymentsByDateHandler,
        GetPaymentHandler getPaymentHandler,
        GetPaymentEventsHandler getPaymentEventsHandler,
        ILogger<PaymentManagementFunction> logger)
    {
        _getPaymentsByDateHandler = getPaymentsByDateHandler;
        _getPaymentHandler = getPaymentHandler;
        _getPaymentEventsHandler = getPaymentEventsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/payments?date=YYYY-MM-DD
    // -------------------------------------------------------------------------

    [Function("AdminGetPaymentsByDate")]
    [OpenApiOperation(operationId: "AdminGetPaymentsByDate", tags: new[] { "Payment Reporting" }, Summary = "Retrieve all payments for a specific date")]
    [OpenApiParameter(name: "date", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Date in YYYY-MM-DD format (UTC)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<AdminPaymentListItemResponse>), Description = "OK – list of payments with event counts")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – missing or invalid date")]
    public async Task<HttpResponseData> GetPaymentsByDate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/payments")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var dateStr = req.Url.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p.Length == 2 && string.Equals(p[0], "date", StringComparison.OrdinalIgnoreCase))
            ?[1];

        if (string.IsNullOrWhiteSpace(dateStr) || !DateOnly.TryParse(Uri.UnescapeDataString(dateStr), out var date))
            return await req.BadRequestAsync("The 'date' query parameter is required and must be in YYYY-MM-DD format.");

        try
        {
            var query = new GetPaymentsByDateQuery(date);
            var result = await _getPaymentsByDateHandler.HandleAsync(query, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for date {Date}", date);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/payments/{paymentId}
    // -------------------------------------------------------------------------

    [Function("AdminGetPayment")]
    [OpenApiOperation(operationId: "AdminGetPayment", tags: new[] { "Payment Reporting" }, Summary = "Retrieve a single payment record by ID")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The payment's unique identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminPaymentResponse), Description = "OK – returns the payment record")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – payment does not exist")]
    public async Task<HttpResponseData> GetPayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/payments/{paymentId:guid}")] HttpRequestData req,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetPaymentQuery(paymentId);
            var result = await _getPaymentHandler.HandleAsync(query, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/payments/{paymentId}/events
    // -------------------------------------------------------------------------

    [Function("AdminGetPaymentEvents")]
    [OpenApiOperation(operationId: "AdminGetPaymentEvents", tags: new[] { "Payment Reporting" }, Summary = "Retrieve all events for a payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The payment's unique identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<AdminPaymentEventResponse>), Description = "OK – list of payment events in chronological order")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – payment does not exist")]
    public async Task<HttpResponseData> GetPaymentEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/payments/{paymentId:guid}/events")] HttpRequestData req,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetPaymentEventsQuery(paymentId);
            var result = await _getPaymentEventsHandler.HandleAsync(query, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events for payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }
}
