using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Identity.Application.CreateAccount;
using ReservationSystem.Microservices.Identity.Application.DeleteAccount;
using ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;
using ReservationSystem.Microservices.Identity.Application.GetAccount;
using ReservationSystem.Microservices.Identity.Application.GetAccountByEmail;
using ReservationSystem.Shared.Business.Security;
using ReservationSystem.Microservices.Identity.Application.SetPassword;
using ReservationSystem.Microservices.Identity.Application.UpdateAccount;
using ReservationSystem.Microservices.Identity.Application.VerifyEmail;
using ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;
using ReservationSystem.Microservices.Identity.Models.Mappers;
using ReservationSystem.Microservices.Identity.Models.Requests;
using ReservationSystem.Microservices.Identity.Models.Responses;
using ReservationSystem.Microservices.Identity.Validation;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Identity.Functions;

/// <summary>
/// HTTP-triggered functions for user account management endpoints.
/// Handles account creation, deletion, email verification, and email change.
/// </summary>
public sealed class AccountFunction
{
    private readonly CreateAccountHandler _createAccountHandler;
    private readonly DeleteAccountHandler _deleteAccountHandler;
    private readonly GetAccountHandler _getAccountHandler;
    private readonly GetAccountByEmailHandler _getAccountByEmailHandler;
    private readonly UpdateAccountHandler _updateAccountHandler;
    private readonly SetPasswordHandler _setPasswordHandler;
    private readonly VerifyEmailHandler _verifyEmailHandler;
    private readonly EmailChangeRequestHandler _emailChangeRequestHandler;
    private readonly VerifyEmailChangeHandler _verifyEmailChangeHandler;
    private readonly ILogger<AccountFunction> _logger;

    public AccountFunction(
        CreateAccountHandler createAccountHandler,
        DeleteAccountHandler deleteAccountHandler,
        GetAccountHandler getAccountHandler,
        GetAccountByEmailHandler getAccountByEmailHandler,
        UpdateAccountHandler updateAccountHandler,
        SetPasswordHandler setPasswordHandler,
        VerifyEmailHandler verifyEmailHandler,
        EmailChangeRequestHandler emailChangeRequestHandler,
        VerifyEmailChangeHandler verifyEmailChangeHandler,
        ILogger<AccountFunction> logger)
    {
        _createAccountHandler = createAccountHandler;
        _deleteAccountHandler = deleteAccountHandler;
        _getAccountHandler = getAccountHandler;
        _getAccountByEmailHandler = getAccountByEmailHandler;
        _updateAccountHandler = updateAccountHandler;
        _setPasswordHandler = setPasswordHandler;
        _verifyEmailHandler = verifyEmailHandler;
        _emailChangeRequestHandler = emailChangeRequestHandler;
        _verifyEmailChangeHandler = verifyEmailChangeHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts
    // -------------------------------------------------------------------------

    [Function("CreateAccount")]
    [OpenApiOperation(operationId: "CreateAccount", tags: new[] { "Accounts" }, Summary = "Create a new user account")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateAccountRequest), Required = true, Description = "Account creation request: email, password")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateAccountResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – email already registered")]
    public async Task<HttpResponseData> CreateAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateAccountRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = IdentityValidator.ValidateCreateAccount(request.Email, request.Password);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        try
        {
            var command = IdentityMapper.ToCommand(request);
            var result = await _createAccountHandler.HandleAsync(command, cancellationToken);

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json");
            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(result, SharedJsonOptions.CamelCase));
            return httpResponse;
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("An account with this email already exists.");
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/accounts/{userAccountId:guid}
    // -------------------------------------------------------------------------

    [Function("GetAccount")]
    [OpenApiOperation(operationId: "GetAccount", tags: new[] { "Accounts" }, Summary = "Get a user account summary by ID")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AccountSummaryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/accounts/{userAccountId:guid}")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var account = await _getAccountHandler.HandleAsync(new GetAccountQuery(userAccountId), cancellationToken);

        if (account is null)
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");

        var response = new AccountSummaryResponse
        {
            UserAccountId = account.UserAccountId,
            Email = account.Email,
            IsEmailVerified = account.IsEmailVerified,
            IsLocked = account.IsLocked,
            FailedLoginAttempts = account.FailedLoginAttempts,
            LastLoginAt = account.LastLoginAt,
            PasswordChangedAt = account.PasswordChangedAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // GET /v1/accounts/by-email/{email}
    // -------------------------------------------------------------------------

    [Function("GetAccountByEmail")]
    [OpenApiOperation(operationId: "GetAccountByEmail", tags: new[] { "Accounts" }, Summary = "Get a user account summary by email address")]
    [OpenApiParameter(name: "email", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Email address")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AccountSummaryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetAccountByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/accounts/by-email/{email}")] HttpRequestData req,
        string email,
        CancellationToken cancellationToken)
    {
        var account = await _getAccountByEmailHandler.HandleAsync(new GetAccountByEmailQuery(email), cancellationToken);

        if (account is null)
            return await req.NotFoundAsync($"No user account found for email '{email}'.");

        var response = new AccountSummaryResponse
        {
            UserAccountId = account.UserAccountId,
            Email = account.Email,
            IsEmailVerified = account.IsEmailVerified,
            IsLocked = account.IsLocked,
            FailedLoginAttempts = account.FailedLoginAttempts,
            LastLoginAt = account.LastLoginAt,
            PasswordChangedAt = account.PasswordChangedAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/accounts/{userAccountId:guid}
    // -------------------------------------------------------------------------

    [Function("UpdateAccount")]
    [OpenApiOperation(operationId: "UpdateAccount", tags: new[] { "Accounts" }, Summary = "Update account email or locked status (admin)")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateAccountRequest), Required = true, Description = "Fields to update (email, isLocked)")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – email already registered")]
    public async Task<HttpResponseData> UpdateAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/accounts/{userAccountId:guid}")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateAccountRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request!.Email is not null)
        {
            var emailErrors = IdentityValidator.ValidateEmailField(request.Email);
            if (emailErrors.Count > 0)
                return await req.BadRequestAsync(string.Join(" ", emailErrors));
        }

        try
        {
            var command = new UpdateAccountCommand(userAccountId, request.Email, request.IsLocked);
            await _updateAccountHandler.HandleAsync(command, cancellationToken);
            return req.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("The email address is already registered to another account.");
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/accounts/{userAccountId:guid}
    // -------------------------------------------------------------------------

    [Function("DeleteAccount")]
    [OpenApiOperation(operationId: "DeleteAccount", tags: new[] { "Accounts" }, Summary = "Delete a user account")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/accounts/{userAccountId:guid}")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteAccountCommand(userAccountId);
            await _deleteAccountHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId:guid}/set-password
    // -------------------------------------------------------------------------

    [Function("SetPassword")]
    [OpenApiOperation(operationId: "SetPassword", tags: new[] { "Accounts" }, Summary = "Set password directly on an account (admin)")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SetPasswordRequest), Required = true, Description = "New password")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Password set")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{userAccountId:guid}/set-password")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SetPasswordRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = IdentityValidator.ValidatePasswordField(request!.NewPassword);
        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        try
        {
            var passwordHash = PasswordHasher.HashPassword(request.NewPassword!);
            var command = new SetPasswordCommand(userAccountId, passwordHash);
            await _setPasswordHandler.HandleAsync(command, cancellationToken);
            return req.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId:guid}/verify-email
    // -------------------------------------------------------------------------

    [Function("VerifyEmail")]
    [OpenApiOperation(operationId: "VerifyEmail", tags: new[] { "Accounts" }, Summary = "Mark account email as verified")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> VerifyEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new VerifyEmailCommand(userAccountId);
            await _verifyEmailHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId:guid}/email/change-request
    // -------------------------------------------------------------------------

    [Function("EmailChangeRequest")]
    [OpenApiOperation(operationId: "EmailChangeRequest", tags: new[] { "Accounts" }, Summary = "Request an email address change")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "User account ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(EmailChangeRequest), Required = true, Description = "Email change request: newEmail")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Accepted, Description = "Accepted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> EmailChangeRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{userAccountId:guid}/email/change-request")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<EmailChangeRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var emailErrors = IdentityValidator.ValidateEmailField(request.NewEmail);

        if (emailErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", emailErrors));

        try
        {
            var command = new EmailChangeRequestCommand(userAccountId, request.NewEmail);
            await _emailChangeRequestHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("The new email address is already registered to another account.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/email/verify
    // -------------------------------------------------------------------------

    [Function("VerifyEmailChange")]
    [OpenApiOperation(operationId: "VerifyEmailChange", tags: new[] { "Accounts" }, Summary = "Verify email change with token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VerifyEmailChangeRequest), Required = true, Description = "Verification request: token")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – invalid or expired token")]
    public async Task<HttpResponseData> VerifyEmailChange(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/email/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<VerifyEmailChangeRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var tokenErrors = IdentityValidator.ValidateRequiredToken(request.Token, "token");
        var emailErrors = IdentityValidator.ValidateEmailField(request.NewEmail);
        var allErrors = tokenErrors.Concat(emailErrors).ToList();

        if (allErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", allErrors));

        try
        {
            var command = new VerifyEmailChangeCommand(request.Token, request.NewEmail);
            await _verifyEmailChangeHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (ArgumentException)
        {
            return await req.BadRequestAsync("Invalid or expired verification token.");
        }
    }
}
