using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.User.Application.AddUser;
using ReservationSystem.Microservices.User.Application.GetAllUsers;
using ReservationSystem.Microservices.User.Application.GetUser;
using ReservationSystem.Microservices.User.Application.UpdateUser;
using ReservationSystem.Microservices.User.Application.SetUserStatus;
using ReservationSystem.Microservices.User.Application.UnlockUser;
using ReservationSystem.Microservices.User.Application.DeleteUser;
using ReservationSystem.Microservices.User.Application.ResetPassword;
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
/// Handles user CRUD operations.
/// </summary>
public sealed class UserFunction
{
    private readonly AddUserHandler _addUserHandler;
    private readonly GetAllUsersHandler _getAllUsersHandler;
    private readonly GetUserHandler _getUserHandler;
    private readonly UpdateUserHandler _updateUserHandler;
    private readonly SetUserStatusHandler _setUserStatusHandler;
    private readonly UnlockUserHandler _unlockUserHandler;
    private readonly ResetPasswordHandler _resetPasswordHandler;
    private readonly DeleteUserHandler _deleteUserHandler;
    private readonly ILogger<UserFunction> _logger;

    public UserFunction(
        AddUserHandler addUserHandler,
        GetAllUsersHandler getAllUsersHandler,
        GetUserHandler getUserHandler,
        UpdateUserHandler updateUserHandler,
        SetUserStatusHandler setUserStatusHandler,
        UnlockUserHandler unlockUserHandler,
        ResetPasswordHandler resetPasswordHandler,
        DeleteUserHandler deleteUserHandler,
        ILogger<UserFunction> logger)
    {
        _addUserHandler = addUserHandler;
        _getAllUsersHandler = getAllUsersHandler;
        _getUserHandler = getUserHandler;
        _updateUserHandler = updateUserHandler;
        _setUserStatusHandler = setUserStatusHandler;
        _unlockUserHandler = unlockUserHandler;
        _resetPasswordHandler = resetPasswordHandler;
        _deleteUserHandler = deleteUserHandler;
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

    // -------------------------------------------------------------------------
    // GET /v1/users/{userId}
    // -------------------------------------------------------------------------

    [Function("GetUser")]
    [OpenApiOperation(operationId: "GetUser", tags: new[] { "Users" }, Summary = "Retrieve a single employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UserResponse), Description = "OK – returns the user account (password excluded)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> GetUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(userId);
        var result = await _getUserHandler.HandleAsync(query, cancellationToken);

        if (result is null)
            return await req.NotFoundAsync("User not found.");

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/users/{userId}
    // -------------------------------------------------------------------------

    [Function("UpdateUser")]
    [OpenApiOperation(operationId: "UpdateUser", tags: new[] { "Users" }, Summary = "Update an employee user account's profile")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateUserRequest), Required = true, Description = "Fields to update (all optional)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – update successful")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – email already in use")]
    public async Task<HttpResponseData> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateUserRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = UserValidator.ValidateUpdateUser(
            request!.FirstName, request.LastName, request.Email);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        try
        {
            var command = UserMapper.ToCommand(userId, request);
            var found = await _updateUserHandler.HandleAsync(command, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/users/{userId}/status
    // -------------------------------------------------------------------------

    [Function("SetUserStatus")]
    [OpenApiOperation(operationId: "SetUserStatus", tags: new[] { "Users" }, Summary = "Activate or deactivate an employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SetUserStatusRequest), Required = true, Description = "Account status")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – status updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> SetUserStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/users/{userId:guid}/status")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SetUserStatusRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var command = UserMapper.ToCommand(userId, request!);
        var found = await _setUserStatusHandler.HandleAsync(command, cancellationToken);

        if (!found)
            return await req.NotFoundAsync("User not found.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /v1/users/{userId}/unlock
    // -------------------------------------------------------------------------

    [Function("UnlockUser")]
    [OpenApiOperation(operationId: "UnlockUser", tags: new[] { "Users" }, Summary = "Unlock a locked employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – account unlocked")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> UnlockUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users/{userId:guid}/unlock")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var command = UserMapper.ToUnlockCommand(userId);
        var found = await _unlockUserHandler.HandleAsync(command, cancellationToken);

        if (!found)
            return await req.NotFoundAsync("User not found.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /v1/users/{userId}/reset-password
    // -------------------------------------------------------------------------

    [Function("ResetPassword")]
    [OpenApiOperation(operationId: "ResetPassword", tags: new[] { "Users" }, Summary = "Reset an employee user's password")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ResetPasswordRequest), Required = true, Description = "New password")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – password reset")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users/{userId:guid}/reset-password")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ResetPasswordRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = UserValidator.ValidateResetPassword(request!.NewPassword);
        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = UserMapper.ToCommand(userId, request);
        var found = await _resetPasswordHandler.HandleAsync(command, cancellationToken);

        if (!found)
            return await req.NotFoundAsync("User not found.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/users/{userId}
    // -------------------------------------------------------------------------

    [Function("DeleteUser")]
    [OpenApiOperation(operationId: "DeleteUser", tags: new[] { "Users" }, Summary = "Permanently delete an employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – user deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var command = UserMapper.ToDeleteCommand(userId);
        var found = await _deleteUserHandler.HandleAsync(command, cancellationToken);

        if (!found)
            return await req.NotFoundAsync("User not found.");

        return req.NoContent();
    }
}
