using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.User.Application.AddUser;
using ReservationSystem.Microservices.User.Application.GetAllUsers;
using ReservationSystem.Microservices.User.Models.Mappers;
using ReservationSystem.Microservices.User.Models.Requests;
using ReservationSystem.Microservices.User.Models.Responses;
using ReservationSystem.Microservices.User.Validation;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.User.Functions;

/// <summary>
/// HTTP-triggered functions for employee user management.
/// Handles user creation and listing.
/// </summary>
public sealed class UserFunction
{
    private readonly AddUserHandler _addUserHandler;
    private readonly GetAllUsersHandler _getAllUsersHandler;
    private readonly ILogger<UserFunction> _logger;

    public UserFunction(
        AddUserHandler addUserHandler,
        GetAllUsersHandler getAllUsersHandler,
        ILogger<UserFunction> logger)
    {
        _addUserHandler = addUserHandler;
        _getAllUsersHandler = getAllUsersHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/users
    // -------------------------------------------------------------------------

    [Function("AddUser")]
    [OpenApiOperation(operationId: "AddUser", tags: new[] { "Users" }, Summary = "Create a new employee user account")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AddUserRequest), Required = true, Description = "User creation request: username, email, password, firstName, lastName")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(AddUserResponse), Description = "Created – returns the new userId")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – username or email already exists")]
    public async Task<HttpResponseData> AddUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AddUserRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = UserValidator.ValidateAddUser(
            request!.Username, request.Email, request.Password, request.FirstName, request.LastName);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        try
        {
            var command = UserMapper.ToCommand(request);
            var result = await _addUserHandler.HandleAsync(command, cancellationToken);

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json");
            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(result, SharedJsonOptions.CamelCase));
            return httpResponse;
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/users
    // -------------------------------------------------------------------------

    [Function("GetAllUsers")]
    [OpenApiOperation(operationId: "GetAllUsers", tags: new[] { "Users" }, Summary = "Retrieve all employee user accounts")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<UserResponse>), Description = "OK – returns list of user accounts (passwords excluded)")]
    public async Task<HttpResponseData> GetAllUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = new GetAllUsersQuery();
        var result = await _getAllUsersHandler.HandleAsync(query, cancellationToken);
        return await req.OkJsonAsync(result);
    }
}
