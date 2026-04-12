using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Text;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for staff-facing seat pricing CRUD.
/// All routes require a valid staff JWT token with the "User" role claim,
/// enforced by <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>.
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class AdminSeatPricingFunction
{
    private readonly SeatServiceClient _seatServiceClient;
    private readonly ILogger<AdminSeatPricingFunction> _logger;

    public AdminSeatPricingFunction(
        SeatServiceClient seatServiceClient,
        ILogger<AdminSeatPricingFunction> logger)
    {
        _seatServiceClient = seatServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/seat-pricing
    // -------------------------------------------------------------------------

    [Function("AdminGetAllSeatPricings")]
    [OpenApiOperation(operationId: "AdminGetAllSeatPricings", tags: new[] { "Admin Seat Pricing" }, Summary = "List all seat pricing rules (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<SeatPricingDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var pricings = await _seatServiceClient.GetAllSeatPricingsAsync(cancellationToken);
        return await req.OkJsonAsync(pricings);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("AdminGetSeatPricingById")]
    [OpenApiOperation(operationId: "AdminGetSeatPricingById", tags: new[] { "Admin Seat Pricing" }, Summary = "Get a seat pricing rule by ID (staff)")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Seat pricing rule ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatPricingDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var pricing = await _seatServiceClient.GetSeatPricingByIdAsync(seatPricingId, cancellationToken);
        if (pricing is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(pricing);
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/seat-pricing
    // -------------------------------------------------------------------------

    [Function("AdminCreateSeatPricing")]
    [OpenApiOperation(operationId: "AdminCreateSeatPricing", tags: new[] { "Admin Seat Pricing" }, Summary = "Create a new seat pricing rule (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateSeatPricingRequestDto), Required = true, Description = "Seat pricing rule to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(SeatPricingDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateSeatPricingRequestDto>(_logger, cancellationToken);
        if (error is not null) return error;

        var (result, status, errorBody) = await _seatServiceClient.CreateSeatPricingAsync(request, cancellationToken);

        if (status == HttpStatusCode.Conflict)
            return await RelayErrorAsync(req, HttpStatusCode.Conflict, errorBody);

        if (status == HttpStatusCode.BadRequest)
            return await RelayErrorAsync(req, HttpStatusCode.BadRequest, errorBody);

        if (!IsSuccess(status) || result is null)
            return await RelayErrorAsync(req, HttpStatusCode.InternalServerError, errorBody);

        _logger.LogInformation("Created seat pricing rule {SeatPricingId}", result.SeatPricingId);
        return await req.CreatedAsync($"/v1/admin/seat-pricing/{result.SeatPricingId}", result);
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateSeatPricing")]
    [OpenApiOperation(operationId: "AdminUpdateSeatPricing", tags: new[] { "Admin Seat Pricing" }, Summary = "Update a seat pricing rule (staff)")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Seat pricing rule ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSeatPricingRequestDto), Required = true, Description = "Fields to update")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatPricingDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateSeatPricingRequestDto>(_logger, cancellationToken);
        if (error is not null) return error;

        var (result, status, errorBody) = await _seatServiceClient.UpdateSeatPricingAsync(seatPricingId, request, cancellationToken);

        if (status == HttpStatusCode.NotFound)
            return req.CreateResponse(HttpStatusCode.NotFound);

        if (status == HttpStatusCode.BadRequest)
            return await RelayErrorAsync(req, HttpStatusCode.BadRequest, errorBody);

        if (!IsSuccess(status) || result is null)
            return await RelayErrorAsync(req, HttpStatusCode.InternalServerError, errorBody);

        _logger.LogInformation("Updated seat pricing rule {SeatPricingId}", seatPricingId);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("AdminDeleteSeatPricing")]
    [OpenApiOperation(operationId: "AdminDeleteSeatPricing", tags: new[] { "Admin Seat Pricing" }, Summary = "Delete a seat pricing rule (staff)")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Seat pricing rule ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var status = await _seatServiceClient.DeleteSeatPricingAsync(seatPricingId, cancellationToken);

        if (status == HttpStatusCode.NotFound)
            return req.CreateResponse(HttpStatusCode.NotFound);

        if (!IsSuccess(status))
            return req.CreateResponse(HttpStatusCode.InternalServerError);

        _logger.LogInformation("Deleted seat pricing rule {SeatPricingId}", seatPricingId);
        return req.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsSuccess(HttpStatusCode status) =>
        (int)status is >= 200 and < 300;

    private static async Task<HttpResponseData> RelayErrorAsync(HttpRequestData req, HttpStatusCode status, string? body)
    {
        var response = req.CreateResponse(status);
        if (!string.IsNullOrEmpty(body))
        {
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(body, Encoding.UTF8);
        }
        return response;
    }
}
