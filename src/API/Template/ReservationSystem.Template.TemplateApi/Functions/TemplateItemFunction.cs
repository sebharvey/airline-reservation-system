using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Template.TemplateApi.Application.UseCases.CreateTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.DeleteTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetAllTemplateItems;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetTemplateItem;
using ReservationSystem.Template.TemplateApi.Models.Mappers;
using ReservationSystem.Template.TemplateApi.Models.Requests;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Template.TemplateApi.Functions;

/// <summary>
/// HTTP-triggered functions for the TemplateItem resource.
/// This is the "Presentation" layer in clean architecture — it translates
/// HTTP concerns (request parsing, status codes, serialisation) into
/// application-layer calls and back again.
/// </summary>
public sealed class TemplateItemFunction
{
    private readonly GetTemplateItemHandler _getHandler;
    private readonly GetAllTemplateItemsHandler _getAllHandler;
    private readonly CreateTemplateItemHandler _createHandler;
    private readonly DeleteTemplateItemHandler _deleteHandler;
    private readonly ILogger<TemplateItemFunction> _logger;

    public TemplateItemFunction(
        GetTemplateItemHandler getHandler,
        GetAllTemplateItemsHandler getAllHandler,
        CreateTemplateItemHandler createHandler,
        DeleteTemplateItemHandler deleteHandler,
        ILogger<TemplateItemFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/template-items
    // -------------------------------------------------------------------------

    [Function("GetAllTemplateItems")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/template-items")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var items = await _getAllHandler.HandleAsync(new GetAllTemplateItemsQuery(), cancellationToken);
        var body = TemplateItemMapper.ToResponse(items);
        return await req.OkJsonAsync(body);
    }

    // -------------------------------------------------------------------------
    // GET /v1/template-items/{id}
    // -------------------------------------------------------------------------

    [Function("GetTemplateItem")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/template-items/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _getHandler.HandleAsync(new GetTemplateItemQuery(id), cancellationToken);

        if (item is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(TemplateItemMapper.ToResponse(item));
    }

    // -------------------------------------------------------------------------
    // POST /v1/template-items
    // -------------------------------------------------------------------------

    [Function("CreateTemplateItem")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/template-items")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateTemplateItemRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateTemplateItemRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateTemplateItem request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("The 'name' field is required.");

        var command = TemplateItemMapper.ToCommand(request);
        var created = await _createHandler.HandleAsync(command, cancellationToken);
        var response = TemplateItemMapper.ToResponse(created);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/template-items/{created.Id}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(response, SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/template-items/{id}
    // -------------------------------------------------------------------------

    [Function("DeleteTemplateItem")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/template-items/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteTemplateItemCommand(id), cancellationToken);

        return deleted
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }

}
