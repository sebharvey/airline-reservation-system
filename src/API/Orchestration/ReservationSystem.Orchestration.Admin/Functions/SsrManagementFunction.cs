using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Requests;
using ReservationSystem.Orchestration.Admin.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Orchestration.Admin.Functions;

/// <summary>
/// HTTP-triggered functions for admin SSR catalogue management.
/// All function names start with "Admin" so that TerminalAuthenticationMiddleware
/// validates the staff JWT token and role claim.
/// Orchestrates calls to the Order microservice.
/// </summary>
public sealed class SsrManagementFunction
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<SsrManagementFunction> _logger;

    public SsrManagementFunction(OrderServiceClient orderServiceClient, ILogger<SsrManagementFunction> logger)
    {
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/ssr
    // -------------------------------------------------------------------------

    [Function("AdminGetSsrOptions")]
    [OpenApiOperation(operationId: "AdminGetSsrOptions", tags: new[] { "SSR Management" }, Summary = "Retrieve all active SSR catalogue entries")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SsrOptionListResponse), Description = "OK – returns list of active SSR options")]
    public async Task<HttpResponseData> GetSsrOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/ssr")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var msResult = await _orderServiceClient.GetSsrOptionsAsync(cancellationToken);

            var response = new SsrOptionListResponse
            {
                SsrOptions = msResult.SsrOptions
                    .Select(o => new SsrOptionSummary
                    {
                        SsrCode = o.SsrCode,
                        Label = o.Label,
                        Category = o.Category
                    })
                    .ToList()
                    .AsReadOnly()
            };

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SSR options");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/ssr
    // -------------------------------------------------------------------------

    [Function("AdminCreateSsrOption")]
    [OpenApiOperation(operationId: "AdminCreateSsrOption", tags: new[] { "SSR Management" }, Summary = "Create a new SSR catalogue entry")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateSsrOptionRequest), Required = true, Description = "SSR creation request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(SsrOptionResponse), Description = "Created – returns the new SSR entry")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – SSR code already exists")]
    public async Task<HttpResponseData> CreateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/ssr")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateSsrOptionRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.SsrCode) ||
            string.IsNullOrWhiteSpace(request.Label) ||
            string.IsNullOrWhiteSpace(request.Category))
        {
            return await req.BadRequestAsync("The fields 'ssrCode', 'label', and 'category' are required.");
        }

        try
        {
            var body = new
            {
                ssrCode = request.SsrCode.ToUpperInvariant(),
                label = request.Label,
                category = request.Category
            };

            var result = await _orderServiceClient.CreateSsrOptionAsync(body, cancellationToken);

            var response = new SsrOptionResponse
            {
                SsrCatalogueId = result.SsrCatalogueId,
                SsrCode = result.SsrCode,
                Label = result.Label,
                Category = result.Category,
                IsActive = result.IsActive
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json");
            await httpResponse.WriteStringAsync(
                System.Text.Json.JsonSerializer.Serialize(response, ReservationSystem.Shared.Common.Json.SharedJsonOptions.CamelCase));
            return httpResponse;
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SSR option");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/ssr/{ssrCode}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateSsrOption")]
    [OpenApiOperation(operationId: "AdminUpdateSsrOption", tags: new[] { "SSR Management" }, Summary = "Update the label or category of an existing SSR catalogue entry")]
    [OpenApiParameter(name: "ssrCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Four-character IATA SSR code")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSsrOptionRequest), Required = true, Description = "Fields to update")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SsrOptionResponse), Description = "OK – returns the updated SSR entry")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – SSR code does not exist")]
    public async Task<HttpResponseData> UpdateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/ssr/{ssrCode}")] HttpRequestData req,
        string ssrCode,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateSsrOptionRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Label) || string.IsNullOrWhiteSpace(request.Category))
            return await req.BadRequestAsync("The fields 'label' and 'category' are required.");

        try
        {
            var body = new { label = request.Label, category = request.Category };
            var result = await _orderServiceClient.UpdateSsrOptionAsync(ssrCode.ToUpperInvariant(), body, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync("SSR code not found.");

            return await req.OkJsonAsync(new SsrOptionResponse
            {
                SsrCatalogueId = result.SsrCatalogueId,
                SsrCode = result.SsrCode,
                Label = result.Label,
                Category = result.Category,
                IsActive = result.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SSR option {SsrCode}", ssrCode);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/ssr/{ssrCode}
    // -------------------------------------------------------------------------

    [Function("AdminDeactivateSsrOption")]
    [OpenApiOperation(operationId: "AdminDeactivateSsrOption", tags: new[] { "SSR Management" }, Summary = "Deactivate an SSR catalogue entry (sets IsActive = false)")]
    [OpenApiParameter(name: "ssrCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Four-character IATA SSR code")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – deactivated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – SSR code does not exist")]
    public async Task<HttpResponseData> DeactivateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/ssr/{ssrCode}")] HttpRequestData req,
        string ssrCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _orderServiceClient.DeactivateSsrOptionAsync(ssrCode.ToUpperInvariant(), cancellationToken);

            if (!found)
                return await req.NotFoundAsync("SSR code not found.");

            return req.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating SSR option {SsrCode}", ssrCode);
            return await req.InternalServerErrorAsync();
        }
    }
}
