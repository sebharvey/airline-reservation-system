using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Offer.Application.CreateFareFamily;
using ReservationSystem.Microservices.Offer.Application.DeleteFareFamily;
using ReservationSystem.Microservices.Offer.Application.GetFareFamily;
using ReservationSystem.Microservices.Offer.Application.GetFareFamilies;
using ReservationSystem.Microservices.Offer.Application.UpdateFareFamily;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Functions;

public sealed class FareFamilyFunction
{
    private readonly GetFareFamiliesHandler _getAllHandler;
    private readonly GetFareFamilyHandler _getHandler;
    private readonly CreateFareFamilyHandler _createHandler;
    private readonly UpdateFareFamilyHandler _updateHandler;
    private readonly DeleteFareFamilyHandler _deleteHandler;
    private readonly ILogger<FareFamilyFunction> _logger;

    public FareFamilyFunction(
        GetFareFamiliesHandler getAllHandler,
        GetFareFamilyHandler getHandler,
        CreateFareFamilyHandler createHandler,
        UpdateFareFamilyHandler updateHandler,
        DeleteFareFamilyHandler deleteHandler,
        ILogger<FareFamilyFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _getHandler = getHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // GET /v1/fare-families
    [Function("GetFareFamilies")]
    [MicroserviceCache("FareFamilies", 5)]
    [OpenApiOperation(operationId: "GetFareFamilies", tags: new[] { "Fare Families" }, Summary = "List all fare families")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object[]), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/fare-families")] HttpRequestData req,
        CancellationToken ct)
    {
        var families = await _getAllHandler.HandleAsync(new GetFareFamiliesQuery(), ct);
        return await req.OkJsonAsync(Models.Mappers.FareFamilyMapper.ToResponseList(families));
    }

    // GET /v1/fare-families/{fareFamilyId}
    [Function("GetFareFamily")]
    [MicroserviceCache("FareFamilies", 5)]
    [OpenApiOperation(operationId: "GetFareFamily", tags: new[] { "Fare Families" }, Summary = "Get a fare family by ID")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId, CancellationToken ct)
    {
        var family = await _getHandler.HandleAsync(new GetFareFamilyQuery(fareFamilyId), ct);

        if (family is null)
            return await req.NotFoundAsync($"FareFamily '{fareFamilyId}' not found.");

        return await req.OkJsonAsync(Models.Mappers.FareFamilyMapper.ToResponse(family));
    }

    // POST /v1/fare-families
    [Function("CreateFareFamily")]
    [MicroserviceCacheInvalidate("FareFamilies")]
    [OpenApiOperation(operationId: "CreateFareFamily", tags: new[] { "Fare Families" }, Summary = "Create a new fare family")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(object), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/fare-families")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CreateFareFamilyCommand(
            Name:         body.GetProperty("name").GetString()!,
            Description:  body.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null,
            DisplayOrder: body.TryGetProperty("displayOrder", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetInt32() : 0);

        try
        {
            var family = await _createHandler.HandleAsync(command, ct);
            return await req.CreatedAsync($"/v1/fare-families/{family.FareFamilyId}", Models.Mappers.FareFamilyMapper.ToResponse(family));
        }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // PUT /v1/fare-families/{fareFamilyId}
    [Function("UpdateFareFamily")]
    [MicroserviceCacheInvalidate("FareFamilies")]
    [OpenApiOperation(operationId: "UpdateFareFamily", tags: new[] { "Fare Families" }, Summary = "Update a fare family")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new UpdateFareFamilyCommand(
            FareFamilyId: fareFamilyId,
            Name:         body.GetProperty("name").GetString()!,
            Description:  body.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null,
            DisplayOrder: body.TryGetProperty("displayOrder", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetInt32() : 0);

        try
        {
            var family = await _updateHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(Models.Mappers.FareFamilyMapper.ToResponse(family));
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // DELETE /v1/fare-families/{fareFamilyId}
    [Function("DeleteFareFamily")]
    [MicroserviceCacheInvalidate("FareFamilies")]
    [OpenApiOperation(operationId: "DeleteFareFamily", tags: new[] { "Fare Families" }, Summary = "Delete a fare family")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId, CancellationToken ct)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteFareFamilyCommand(fareFamilyId), ct);

        if (!deleted)
            return await req.NotFoundAsync($"FareFamily '{fareFamilyId}' not found.");

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
