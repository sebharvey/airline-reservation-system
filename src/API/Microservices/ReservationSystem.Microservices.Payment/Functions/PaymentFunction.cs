using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Application.AuthorisePayment;
using ReservationSystem.Microservices.Payment.Application.RefundPayment;
using ReservationSystem.Microservices.Payment.Application.SettlePayment;
using ReservationSystem.Microservices.Payment.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Payment.Functions;

/// <summary>
/// HTTP-triggered functions for the Payment resource.
/// Translates HTTP concerns into application-layer calls and back.
/// </summary>
public sealed class PaymentFunction
{
    private readonly AuthorisePaymentHandler _authoriseHandler;
    private readonly SettlePaymentHandler _settleHandler;
    private readonly RefundPaymentHandler _refundHandler;
    private readonly ILogger<PaymentFunction> _logger;

    public PaymentFunction(
        AuthorisePaymentHandler authoriseHandler,
        SettlePaymentHandler settleHandler,
        RefundPaymentHandler refundHandler,
        ILogger<PaymentFunction> logger)
    {
        _authoriseHandler = authoriseHandler;
        _settleHandler = settleHandler;
        _refundHandler = refundHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/authorise
    // -------------------------------------------------------------------------

    [Function("AuthorisePayment")]
    public async Task<HttpResponseData> Authorise(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/authorise")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        AuthorisePaymentRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<AuthorisePaymentRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in AuthorisePayment request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (request.CardDetails is null
            || string.IsNullOrWhiteSpace(request.CardDetails.CardNumber)
            || string.IsNullOrWhiteSpace(request.CardDetails.ExpiryDate)
            || string.IsNullOrWhiteSpace(request.CardDetails.Cvv))
            return await req.BadRequestAsync("Card details (cardNumber, expiryDate, cvv) are required.");

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            return await req.BadRequestAsync("The 'currencyCode' field is required.");

        if (string.IsNullOrWhiteSpace(request.PaymentType))
            return await req.BadRequestAsync("The 'paymentType' field is required.");

        if (request.Amount <= 0)
            return await req.BadRequestAsync("The 'amount' must be greater than zero.");

        var command = new AuthorisePaymentCommand(
            request.Amount,
            request.CurrencyCode,
            request.CardDetails.CardNumber,
            request.CardDetails.ExpiryDate,
            request.CardDetails.Cvv,
            request.CardDetails.CardholderName,
            request.PaymentType,
            request.Description);

        try
        {
            var result = await _authoriseHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/payment/{result.PaymentReference}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorise payment");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentReference}/settle
    // -------------------------------------------------------------------------

    [Function("SettlePayment")]
    public async Task<HttpResponseData> Settle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentReference}/settle")] HttpRequestData req,
        string paymentReference,
        CancellationToken cancellationToken)
    {
        SettlePaymentRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<SettlePaymentRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in SettlePayment request for {PaymentReference}", paymentReference);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (request.SettledAmount <= 0)
            return await req.BadRequestAsync("The 'settledAmount' must be greater than zero.");

        var command = new SettlePaymentCommand(paymentReference, request.SettledAmount);

        try
        {
            var result = await _settleHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentReference}' not found or cannot be settled.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to settle payment {PaymentReference}", paymentReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/payment/{paymentReference}/refund
    // -------------------------------------------------------------------------

    [Function("RefundPayment")]
    public async Task<HttpResponseData> Refund(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/payment/{paymentReference}/refund")] HttpRequestData req,
        string paymentReference,
        CancellationToken cancellationToken)
    {
        RefundPaymentRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<RefundPaymentRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in RefundPayment request for {PaymentReference}", paymentReference);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (request.RefundAmount <= 0)
            return await req.BadRequestAsync("The 'refundAmount' must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return await req.BadRequestAsync("The 'reason' field is required.");

        var command = new RefundPaymentCommand(paymentReference, request.RefundAmount, request.Reason);

        try
        {
            var result = await _refundHandler.HandleAsync(command, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Payment '{paymentReference}' not found or cannot be refunded.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refund payment {PaymentReference}", paymentReference);
            return await req.InternalServerErrorAsync();
        }
    }
}
