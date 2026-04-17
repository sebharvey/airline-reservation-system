using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Application.AuthorisePayment;
using ReservationSystem.Microservices.Payment.Application.GetPayment;
using ReservationSystem.Microservices.Payment.Application.GetPaymentEvents;
using ReservationSystem.Microservices.Payment.Application.InitialisePayment;
using ReservationSystem.Microservices.Payment.Application.RefundPayment;
using ReservationSystem.Microservices.Payment.Application.SettlePayment;
using ReservationSystem.Microservices.Payment.Application.VoidPayment;
using ReservationSystem.Microservices.Payment.Models.Requests;
using ReservationSystem.Microservices.Payment.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace ReservationSystem.Microservices.Payment.Functions;

/// <summary>
/// HTTP-triggered functions for the Payment resource.
/// Translates HTTP concerns into application-layer calls and back.
/// </summary>
public sealed class PaymentFunction
{
    private readonly InitialisePaymentHandler _initialiseHandler;
    private readonly AuthorisePaymentHandler _authoriseHandler;
    private readonly SettlePaymentHandler _settleHandler;
    private readonly RefundPaymentHandler _refundHandler;
    private readonly VoidPaymentHandler _voidHandler;
    private readonly GetPaymentHandler _getPaymentHandler;
    private readonly GetPaymentEventsHandler _getPaymentEventsHandler;
    private readonly ILogger<PaymentFunction> _logger;

    public PaymentFunction(
        InitialisePaymentHandler initialiseHandler,
        AuthorisePaymentHandler authoriseHandler,
        SettlePaymentHandler settleHandler,
        RefundPaymentHandler refundHandler,
        VoidPaymentHandler voidHandler,
        GetPaymentHandler getPaymentHandler,
        GetPaymentEventsHandler getPaymentEventsHandler,
        ILogger<PaymentFunction> logger)
    {
        _initialiseHandler = initialiseHandler;
        _authoriseHandler = authoriseHandler;
        _settleHandler = settleHandler;
        _refundHandler = refundHandler;
        _voidHandler = voidHandler;
        _getPaymentHandler = getPaymentHandler;
        _getPaymentEventsHandler = getPaymentEventsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/initialise
    // -------------------------------------------------------------------------

    [Function("InitialisePayment")]
    [OpenApiOperation(operationId: "InitialisePayment", tags: new[] { "Payments" }, Summary = "Initialise a payment with order details")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(InitialisePaymentRequest), Required = true, Description = "Payment initialisation request: order details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(InitialisePaymentResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> Initialise(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/initialise")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<InitialisePaymentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.PaymentType))
            return await req.BadRequestAsync("The 'paymentType' field is required.");

        if (string.IsNullOrWhiteSpace(request.Method))
            return await req.BadRequestAsync("The 'method' field is required.");

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            return await req.BadRequestAsync("The 'currencyCode' field is required.");

        if (request.Amount <= 0)
            return await req.BadRequestAsync("The 'amount' must be greater than zero.");

        var command = new InitialisePaymentCommand(
            request.BookingReference,
            request.PaymentType,
            request.Method,
            request.CurrencyCode,
            request.Amount,
            request.Description);

        try
        {
            var result = await _initialiseHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/payment/{result.PaymentId}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialise payment");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentId}/authorise
    // -------------------------------------------------------------------------

    [Function("AuthorisePayment")]
    [OpenApiOperation(operationId: "AuthorisePayment", tags: new[] { "Payments" }, Summary = "Authorise an initialised payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AuthorisePaymentRequest), Required = true, Description = "Payment authorisation request: cardDetails")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AuthorisePaymentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> Authorise(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentId}/authorise")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        var (request, error) = await req.TryDeserializeBodyAsync<AuthorisePaymentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var cardNumber = string.Empty;
        var expiryDate = string.Empty;
        var cvv = string.Empty;
        var cardholderName = string.Empty;

        if (request.CardDetails is not null)
        {
            if (string.IsNullOrWhiteSpace(request.CardDetails.CardNumber)
                || string.IsNullOrWhiteSpace(request.CardDetails.ExpiryDate)
                || string.IsNullOrWhiteSpace(request.CardDetails.Cvv))
                return await req.BadRequestAsync("Card details (cardNumber, expiryDate, cvv) are required.");

            cardNumber = request.CardDetails.CardNumber.Replace(" ", "").Replace("-", "");

            if (!IsValidLuhn(cardNumber))
                return await req.BadRequestAsync("Card number is invalid.");

            if (!IsValidExpiryDate(request.CardDetails.ExpiryDate))
                return await req.BadRequestAsync("Card expiry date is invalid or in the past.");

            var isAmex = cardNumber.Length >= 2 && cardNumber[0] == '3' && (cardNumber[1] == '4' || cardNumber[1] == '7');
            var expectedCvvLength = isAmex ? 4 : 3;
            cvv = request.CardDetails.Cvv;
            if (cvv.Length != expectedCvvLength || !cvv.All(char.IsDigit))
                return await req.BadRequestAsync($"Security code must be {expectedCvvLength} digits.");

            expiryDate = request.CardDetails.ExpiryDate;
            cardholderName = request.CardDetails.CardholderName ?? string.Empty;
        }

        if (request.Amount.HasValue && request.Amount.Value <= 0)
            return await req.BadRequestAsync("The 'amount' must be greater than zero when provided.");

        var command = new AuthorisePaymentCommand(
            paymentGuid,
            request.Amount,
            cardNumber,
            expiryDate,
            cvv,
            cardholderName);

        try
        {
            var result = await _authoriseHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict authorising payment {PaymentId}", paymentId);
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorise payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentId}/settle
    // -------------------------------------------------------------------------

    [Function("SettlePayment")]
    [OpenApiOperation(operationId: "SettlePayment", tags: new[] { "Payments" }, Summary = "Settle an authorised payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SettlePaymentRequest), Required = true, Description = "Settlement request: settledAmount")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SettlePaymentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Settle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentId}/settle")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        var (request, error) = await req.TryDeserializeBodyAsync<SettlePaymentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.SettledAmount <= 0)
            return await req.BadRequestAsync("The 'settledAmount' must be greater than zero.");

        var command = new SettlePaymentCommand(paymentGuid, request.SettledAmount);

        try
        {
            var result = await _settleHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict settling payment {PaymentId}", paymentId);
            return await req.ConflictAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request settling payment {PaymentId}", paymentId);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to settle payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentId}/void
    // -------------------------------------------------------------------------

    [Function("VoidPayment")]
    [OpenApiOperation(operationId: "VoidPayment", tags: new[] { "Payments" }, Summary = "Void an authorised payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VoidPaymentRequest), Required = false, Description = "Void request: optional reason")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(VoidPaymentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Void(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentId}/void")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        VoidPaymentRequest? request = null;

        try
        {
            if (req.Body.Length > 0)
            {
                request = await JsonSerializer.DeserializeAsync<VoidPaymentRequest>(
                    req.Body, SharedJsonOptions.CamelCase, cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in VoidPayment request for {PaymentId}", paymentId);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        var command = new VoidPaymentCommand(paymentGuid, request?.Reason);

        try
        {
            var result = await _voidHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict voiding payment {PaymentId}", paymentId);
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to void payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentId}/refund
    // -------------------------------------------------------------------------

    [Function("RefundPayment")]
    [OpenApiOperation(operationId: "RefundPayment", tags: new[] { "Payments" }, Summary = "Refund a settled payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RefundPaymentRequest), Required = true, Description = "Refund request: refundAmount, reason")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(RefundPaymentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Refund(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentId}/refund")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        var (request, error) = await req.TryDeserializeBodyAsync<RefundPaymentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.RefundAmount <= 0)
            return await req.BadRequestAsync("The 'refundAmount' must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return await req.BadRequestAsync("The 'reason' field is required.");

        var command = new RefundPaymentCommand(paymentGuid, request.RefundAmount, request.Reason);

        try
        {
            var result = await _refundHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict refunding payment {PaymentId}", paymentId);
            return await req.ConflictAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request refunding payment {PaymentId}", paymentId);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refund payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/payment/{paymentId}
    // -------------------------------------------------------------------------

    [Function("GetPayment")]
    [OpenApiOperation(operationId: "GetPayment", tags: new[] { "Payments" }, Summary = "Get a payment record by ID")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PaymentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetPayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/payment/{paymentId}")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        try
        {
            var result = await _getPaymentHandler.HandleAsync(new GetPaymentQuery(paymentGuid), cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/payment/{paymentId}/events
    // -------------------------------------------------------------------------

    [Function("GetPaymentEvents")]
    [OpenApiOperation(operationId: "GetPaymentEvents", tags: new[] { "Payments" }, Summary = "Get all payment events for a payment")]
    [OpenApiParameter(name: "paymentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Payment ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PaymentEventResponse[]), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetPaymentEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/payment/{paymentId}/events")] HttpRequestData req,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(paymentId, out var paymentGuid))
            return await req.BadRequestAsync("Invalid paymentId format — must be a valid UUID.");

        try
        {
            var result = await _getPaymentEventsHandler.HandleAsync(new GetPaymentEventsQuery(paymentGuid), cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve events for payment {PaymentId}", paymentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Luhn algorithm — validates card number check digit
    // -------------------------------------------------------------------------

    private static bool IsValidLuhn(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = cardNumber.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(cardNumber[i]))
                return false;

            var digit = cardNumber[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    // -------------------------------------------------------------------------
    // Expiry date — validates MM/YY format and that the card has not expired
    // -------------------------------------------------------------------------

    private static bool IsValidExpiryDate(string expiryDate)
    {
        if (string.IsNullOrWhiteSpace(expiryDate))
            return false;

        var parts = expiryDate.Split('/');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year))
            return false;

        if (month < 1 || month > 12)
            return false;

        if (year < 100)
            year += 2000;

        var now = DateTime.UtcNow;

        // Card is valid through the end of the expiry month
        return year > now.Year || (year == now.Year && month >= now.Month);
    }
}
