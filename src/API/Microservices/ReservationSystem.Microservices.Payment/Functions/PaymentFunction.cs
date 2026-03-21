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

        var command = new AuthorisePaymentCommand(
            request.BookingReference,
            request.PaymentType,
            request.Method,
            request.CardType,
            request.CardLast4,
            request.CurrencyCode,
            request.Amount,
            request.Description);

        var result = await _authoriseHandler.HandleAsync(command, cancellationToken);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(result, SharedJsonOptions.CamelCase));
        return httpResponse;
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

        var command = new SettlePaymentCommand(paymentReference, request.Amount);

        var result = await _settleHandler.HandleAsync(command, cancellationToken);

        return await req.OkJsonAsync(result);
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

        var command = new RefundPaymentCommand(paymentReference, request.Amount, request.Notes);

        var result = await _refundHandler.HandleAsync(command, cancellationToken);

        return await req.OkJsonAsync(result);
    }
}
