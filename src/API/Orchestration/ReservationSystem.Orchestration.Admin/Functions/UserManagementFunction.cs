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
/// HTTP-triggered functions for admin user management.
/// All function names start with "Admin" so that TerminalAuthenticationMiddleware
/// validates the staff JWT token and role claim.
/// Orchestrates calls to the User microservice.
/// </summary>
public sealed class UserManagementFunction
{
    private readonly UserServiceClient _userServiceClient;
    private readonly ILogger<UserManagementFunction> _logger;

    public UserManagementFunction(UserServiceClient userServiceClient, ILogger<UserManagementFunction> logger)
    {
        _userServiceClient = userServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/users
    // -------------------------------------------------------------------------

    [Function("AdminGetAllUsers")]
    [OpenApiOperation(operationId: "AdminGetAllUsers", tags: new[] { "User Management" }, Summary = "Retrieve all employee user accounts")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<UserResponse>), Description = "OK – returns list of user accounts")]
    public async Task<HttpResponseData> GetAllUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userServiceClient.GetAllUsersAsync(cancellationToken);

            var response = users.Select(u => new UserResponse
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            }).ToList();

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/users/{userId}
    // -------------------------------------------------------------------------

    [Function("AdminGetUser")]
    [OpenApiOperation(operationId: "AdminGetUser", tags: new[] { "User Management" }, Summary = "Retrieve a single employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UserResponse), Description = "OK – returns the user account")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> GetUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userServiceClient.GetUserAsync(userId, cancellationToken);

            if (user is null)
                return await req.NotFoundAsync("User not found.");

            var response = new UserResponse
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                IsLocked = user.IsLocked,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt
            };

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/users
    // -------------------------------------------------------------------------

    [Function("AdminCreateUser")]
    [OpenApiOperation(operationId: "AdminCreateUser", tags: new[] { "User Management" }, Summary = "Create a new employee user account")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AddUserRequest), Required = true, Description = "User creation request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(AddUserResponse), Description = "Created – returns the new userId")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – username or email already exists")]
    public async Task<HttpResponseData> CreateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AddUserRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return await req.BadRequestAsync("The fields 'username', 'email', 'password', 'firstName', and 'lastName' are required.");
        }

        try
        {
            var body = new
            {
                username = request.Username,
                email = request.Email,
                password = request.Password,
                firstName = request.FirstName,
                lastName = request.LastName
            };

            var result = await _userServiceClient.CreateUserAsync(body, cancellationToken);

            var response = new AddUserResponse { UserId = result.UserId };
            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json");
            await httpResponse.WriteStringAsync(
                System.Text.Json.JsonSerializer.Serialize(response, ReservationSystem.Shared.Common.Json.SharedJsonOptions.CamelCase));
            return httpResponse;
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/admin/users/{userId}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateUser")]
    [OpenApiOperation(operationId: "AdminUpdateUser", tags: new[] { "User Management" }, Summary = "Update an employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateUserRequest), Required = true, Description = "Fields to update (all optional)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – update successful")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateUserRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var body = new
            {
                firstName = request!.FirstName,
                lastName = request.LastName,
                email = request.Email
            };

            var found = await _userServiceClient.UpdateUserAsync(userId, body, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/admin/users/{userId}/status
    // -------------------------------------------------------------------------

    [Function("AdminSetUserStatus")]
    [OpenApiOperation(operationId: "AdminSetUserStatus", tags: new[] { "User Management" }, Summary = "Activate or deactivate an employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SetUserStatusRequest), Required = true, Description = "Account status")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – status updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Forbidden, Description = "Forbidden – cannot deactivate your own account")]
    public async Task<HttpResponseData> SetUserStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId:guid}/status")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SetUserStatusRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            if (!request!.IsActive &&
                req.FunctionContext.Items.TryGetValue("StaffUserId", out var staffIdObj) &&
                staffIdObj is string staffId &&
                Guid.TryParse(staffId, out var staffUserId) &&
                staffUserId == userId)
            {
                return await req.ForbiddenAsync("You cannot deactivate your own account.");
            }

            var found = await _userServiceClient.SetUserStatusAsync(userId, request.IsActive, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user status {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/users/{userId}/unlock
    // -------------------------------------------------------------------------

    [Function("AdminUnlockUser")]
    [OpenApiOperation(operationId: "AdminUnlockUser", tags: new[] { "User Management" }, Summary = "Unlock a locked employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – account unlocked")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> UnlockUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId:guid}/unlock")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _userServiceClient.UnlockUserAsync(userId, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/users/{userId}/reset-password
    // -------------------------------------------------------------------------

    [Function("AdminResetPassword")]
    [OpenApiOperation(operationId: "AdminResetPassword", tags: new[] { "User Management" }, Summary = "Reset an employee user's password")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ResetPasswordRequest), Required = true, Description = "New password")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – password reset")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – validation error")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    public async Task<HttpResponseData> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId:guid}/reset-password")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ResetPasswordRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.NewPassword))
            return await req.BadRequestAsync("The 'newPassword' field is required.");

        try
        {
            var found = await _userServiceClient.ResetPasswordAsync(userId, request.NewPassword, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/users/{userId}
    // -------------------------------------------------------------------------

    [Function("AdminDeleteUser")]
    [OpenApiOperation(operationId: "AdminDeleteUser", tags: new[] { "User Management" }, Summary = "Permanently delete an employee user account")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user's unique identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content – user deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found – user does not exist")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Forbidden, Description = "Forbidden – cannot delete your own account")]
    public async Task<HttpResponseData> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/users/{userId:guid}")] HttpRequestData req,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (req.FunctionContext.Items.TryGetValue("StaffUserId", out var staffIdObj) &&
                staffIdObj is string staffId &&
                Guid.TryParse(staffId, out var staffUserId) &&
                staffUserId == userId)
            {
                return await req.ForbiddenAsync("You cannot delete your own account.");
            }

            var found = await _userServiceClient.DeleteUserAsync(userId, cancellationToken);

            if (!found)
                return await req.NotFoundAsync("User not found.");

            return req.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return await req.InternalServerErrorAsync();
        }
    }
}
