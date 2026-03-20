using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Template.TemplateApi.Application.CreatePerson;
using ReservationSystem.Template.TemplateApi.Application.DeletePerson;
using ReservationSystem.Template.TemplateApi.Application.GetAllPersons;
using ReservationSystem.Template.TemplateApi.Application.GetPerson;
using ReservationSystem.Template.TemplateApi.Application.UpdatePerson;
using ReservationSystem.Template.TemplateApi.Models.Mappers;
using ReservationSystem.Template.TemplateApi.Models.Requests;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Template.TemplateApi.Functions;

/// <summary>
/// HTTP-triggered functions for the Person resource (CRUD over [dbo].[Persons]).
/// This is the "Presentation" layer in clean architecture — it translates
/// HTTP concerns (request parsing, status codes, serialisation) into
/// application-layer calls and back again.
///
/// Routes:
///   GET    /v1/persons          — list all persons
///   GET    /v1/persons/{id}     — get single person
///   POST   /v1/persons          — create person
///   PUT    /v1/persons/{id}     — update person
///   DELETE /v1/persons/{id}     — delete person
/// </summary>
public sealed class PersonFunction
{
    private readonly GetPersonHandler _getHandler;
    private readonly GetAllPersonsHandler _getAllHandler;
    private readonly CreatePersonHandler _createHandler;
    private readonly UpdatePersonHandler _updateHandler;
    private readonly DeletePersonHandler _deleteHandler;
    private readonly ILogger<PersonFunction> _logger;

    public PersonFunction(
        GetPersonHandler getHandler,
        GetAllPersonsHandler getAllHandler,
        CreatePersonHandler createHandler,
        UpdatePersonHandler updateHandler,
        DeletePersonHandler deleteHandler,
        ILogger<PersonFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/persons
    // -------------------------------------------------------------------------

    [Function("GetAllPersons")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/persons")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var persons = await _getAllHandler.HandleAsync(new GetAllPersonsQuery(), cancellationToken);
        return await req.OkJsonAsync(PersonMapper.ToResponse(persons));
    }

    // -------------------------------------------------------------------------
    // GET /v1/persons/{id}
    // -------------------------------------------------------------------------

    [Function("GetPerson")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/persons/{id:int}")] HttpRequestData req,
        int id,
        CancellationToken cancellationToken)
    {
        var person = await _getHandler.HandleAsync(new GetPersonQuery(id), cancellationToken);

        if (person is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(PersonMapper.ToResponse(person));
    }

    // -------------------------------------------------------------------------
    // POST /v1/persons
    // -------------------------------------------------------------------------

    [Function("CreatePerson")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/persons")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreatePersonRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreatePersonRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreatePerson request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.LastName))
            return await req.BadRequestAsync("The 'lastName' field is required.");

        var command = PersonMapper.ToCreateCommand(request);
        var created = await _createHandler.HandleAsync(command, cancellationToken);

        if (created is null)
            return await req.ConflictAsync($"A Person with PersonID {command.PersonID} already exists.");

        return await req.CreatedAsync($"/v1/persons/{created.PersonID}", PersonMapper.ToResponse(created));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/persons/{id}
    // -------------------------------------------------------------------------

    [Function("UpdatePerson")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/persons/{id:int}")] HttpRequestData req,
        int id,
        CancellationToken cancellationToken)
    {
        UpdatePersonRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdatePersonRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdatePerson request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.LastName))
            return await req.BadRequestAsync("The 'lastName' field is required.");

        var command = PersonMapper.ToUpdateCommand(id, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(PersonMapper.ToResponse(updated));
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/persons/{id}
    // -------------------------------------------------------------------------

    [Function("DeletePerson")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/persons/{id:int}")] HttpRequestData req,
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeletePersonCommand(id), cancellationToken);

        return deleted
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }
}
