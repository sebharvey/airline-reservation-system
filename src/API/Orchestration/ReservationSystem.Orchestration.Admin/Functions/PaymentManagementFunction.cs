using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
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
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly ILogger<PaymentManagementFunction> _logger;

    public PaymentManagementFunction(PaymentServiceClient paymentServiceClient, ILogger<PaymentManagementFunction> logger)
    {
        _paymentServiceClient = paymentServiceClient;
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
            var payments = await _paymentServiceClient.GetPaymentsByDateAsync(date, cancellationToken);

            var response = payments.Select(p => new AdminPaymentListItemResponse
            {
                PaymentId        = p.PaymentId,
                BookingReference = p.BookingReference,
                PaymentType      = p.PaymentType,
                Method           = p.Method,
                CardType         = p.CardType,
                CardLast4        = p.CardLast4,
                CurrencyCode     = p.CurrencyCode,
                Amount           = p.Amount,
                AuthorisedAmount = p.AuthorisedAmount,
                SettledAmount    = p.SettledAmount,
                Status           = p.Status,
                AuthorisedAt     = p.AuthorisedAt,
                SettledAt        = p.SettledAt,
                Description      = p.Description,
                CreatedAt        = p.CreatedAt,
                UpdatedAt        = p.UpdatedAt,
                EventCount       = p.EventCount
            }).ToList();

            return await req.OkJsonAsync(response);
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
            var payment = await _paymentServiceClient.GetPaymentAsync(paymentId, cancellationToken);

            if (payment is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            var response = new AdminPaymentResponse
            {
                PaymentId        = payment.PaymentId,
                BookingReference = payment.BookingReference,
                PaymentType      = payment.PaymentType,
                Method           = payment.Method,
                CardType         = payment.CardType,
                CardLast4        = payment.CardLast4,
                CurrencyCode     = payment.CurrencyCode,
                Amount           = payment.Amount,
                AuthorisedAmount = payment.AuthorisedAmount,
                SettledAmount    = payment.SettledAmount,
                Status           = payment.Status,
                AuthorisedAt     = payment.AuthorisedAt,
                SettledAt        = payment.SettledAt,
                Description      = payment.Description,
                CreatedAt        = payment.CreatedAt,
                UpdatedAt        = payment.UpdatedAt
            };

            return await req.OkJsonAsync(response);
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
            var payment = await _paymentServiceClient.GetPaymentAsync(paymentId, cancellationToken);

            if (payment is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            var events = await _paymentServiceClient.GetPaymentEventsAsync(paymentId, cancellationToken);

            var response = events.Select(e => new AdminPaymentEventResponse
            {
                PaymentEventId = e.PaymentEventId,
                PaymentId      = e.PaymentId,
                EventType      = e.EventType,
                Amount         = e.Amount,
                CurrencyCode   = e.CurrencyCode,
                Notes          = e.Notes,
                CreatedAt      = e.CreatedAt
            }).ToList();

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events for payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }
}
