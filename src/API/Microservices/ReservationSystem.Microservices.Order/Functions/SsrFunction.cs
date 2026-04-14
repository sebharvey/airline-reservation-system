using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Order.Application.CreateSsrOption;
using ReservationSystem.Microservices.Order.Application.DeactivateSsrOption;
using ReservationSystem.Microservices.Order.Application.GetSsrOptions;
using ReservationSystem.Microservices.Order.Application.UpdateSsrOption;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class SsrFunction
{
    private readonly GetSsrOptionsHandler _getSsrOptionsHandler;
    private readonly CreateSsrOptionHandler _createSsrOptionHandler;
    private readonly UpdateSsrOptionHandler _updateSsrOptionHandler;
    private readonly DeactivateSsrOptionHandler _deactivateSsrOptionHandler;
    private readonly ILogger<SsrFunction> _logger;

    public SsrFunction(
        GetSsrOptionsHandler getSsrOptionsHandler,
        CreateSsrOptionHandler createSsrOptionHandler,
        UpdateSsrOptionHandler updateSsrOptionHandler,
        DeactivateSsrOptionHandler deactivateSsrOptionHandler,
        ILogger<SsrFunction> logger)
    {
        _getSsrOptionsHandler = getSsrOptionsHandler;
        _createSsrOptionHandler = createSsrOptionHandler;
        _updateSsrOptionHandler = updateSsrOptionHandler;
        _deactivateSsrOptionHandler = deactivateSsrOptionHandler;
        _logger = logger;
    }

    // GET /v1/ssr/options
    [Function("GetSsrOptions")]
    [MicroserviceCache(24)]
    [OpenApiOperation(operationId: "GetSsrOptions", tags: new[] { "SSR" }, Summary = "Retrieve all active SSR codes and labels by category")]
    [OpenApiParameter(name: "cabinCode", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Filter SSRs applicable to a specific cabin")]
    [OpenApiParameter(name: "flightNumbers", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Comma-separated flight numbers to filter applicable SSRs")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetSsrOptionsResponse), Description = "OK")]
    public async Task<HttpResponseData> GetSsrOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/ssr/options")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var query = new GetSsrOptionsQuery(qs["cabinCode"], qs["flightNumbers"]);

        var entries = await _getSsrOptionsHandler.HandleAsync(query, cancellationToken);

        var response = new GetSsrOptionsResponse(
            entries.Select(e => new SsrOptionDto(e.SsrCode, e.Label, e.Category))
                   .ToList()
                   .AsReadOnly());

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/ssr/options
    // -------------------------------------------------------------------------

    [Function("CreateSsrOption")]
    [OpenApiOperation(operationId: "CreateSsrOption", tags: new[] { "SSR" }, Summary = "Create a new SSR catalogue entry")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateSsrOptionRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateSsrOptionResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "SSR code already exists")]
    public async Task<HttpResponseData> CreateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ssr/options")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<CreateSsrOptionRequest>(cancellationToken);
        if (body is null) return req.CreateResponse(HttpStatusCode.BadRequest);

        var command = new CreateSsrOptionCommand(body.SsrCode, body.Label, body.Category);
        var entry = await _createSsrOptionHandler.HandleAsync(command, cancellationToken);

        if (entry is null) return req.CreateResponse(HttpStatusCode.Conflict);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new CreateSsrOptionResponse
        {
            SsrCatalogueId = entry.SsrCatalogueId,
            SsrCode = entry.SsrCode,
            Label = entry.Label,
            Category = entry.Category,
            IsActive = entry.IsActive
        }, cancellationToken);
        return response;
    }

    // -------------------------------------------------------------------------
    // PUT /v1/ssr/options/{ssrCode}
    // -------------------------------------------------------------------------

    [Function("UpdateSsrOption")]
    [OpenApiOperation(operationId: "UpdateSsrOption", tags: new[] { "SSR" }, Summary = "Update label or category of an existing SSR catalogue entry")]
    [OpenApiParameter(name: "ssrCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Four-character IATA SSR code")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSsrOptionRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CreateSsrOptionResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "SSR code not found")]
    public async Task<HttpResponseData> UpdateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/ssr/options/{ssrCode}")] HttpRequestData req,
        string ssrCode,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<UpdateSsrOptionRequest>(cancellationToken);
        if (body is null) return req.CreateResponse(HttpStatusCode.BadRequest);

        var command = new UpdateSsrOptionCommand(ssrCode.ToUpperInvariant(), body.Label, body.Category);
        var entry = await _updateSsrOptionHandler.HandleAsync(command, cancellationToken);

        if (entry is null) return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new CreateSsrOptionResponse
        {
            SsrCatalogueId = entry.SsrCatalogueId,
            SsrCode = entry.SsrCode,
            Label = entry.Label,
            Category = entry.Category,
            IsActive = entry.IsActive
        });
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/ssr/options/{ssrCode}
    // -------------------------------------------------------------------------

    [Function("DeactivateSsrOption")]
    [OpenApiOperation(operationId: "DeactivateSsrOption", tags: new[] { "SSR" }, Summary = "Deactivate an SSR code (sets IsActive = false)")]
    [OpenApiParameter(name: "ssrCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Four-character IATA SSR code")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deactivated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "SSR code not found")]
    public async Task<HttpResponseData> DeactivateSsrOption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/ssr/options/{ssrCode}")] HttpRequestData req,
        string ssrCode,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateSsrOptionCommand(ssrCode.ToUpperInvariant());
        var found = await _deactivateSsrOptionHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(found ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }
}
