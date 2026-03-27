using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Application.Login;
using ReservationSystem.Microservices.User.Models.Mappers;
using ReservationSystem.Microservices.User.Models.Requests;
using ReservationSystem.Microservices.User.Models.Responses;
using ReservationSystem.Microservices.User.Validation;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.User.Functions;

/// <summary>
/// HTTP-triggered function for employee authentication.
/// Handles login and issues a JWT access token.
/// </summary>
public sealed class AuthFunction
{
    private readonly LoginHandler _loginHandler;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(LoginHandler loginHandler, ILogger<AuthFunction> logger)
    {
        _loginHandler = loginHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/users/login
    // -------------------------------------------------------------------------

    [Function("Login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" }, Summary = "Authenticate an employee and return a JWT access token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginRequest), Required = true, Description = "Login credentials: username and password")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Description = "OK – returns JWT access token")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – missing fields")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized – invalid credentials")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Forbidden, Description = "Forbidden – account locked or inactive")]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users/login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<LoginRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = UserValidator.ValidateLogin(request!.Username, request.Password);
        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        try
        {
            var command = UserMapper.ToCommand(request);
            var result = await _loginHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (UnauthorizedAccessException)
        {
            return await req.UnauthorizedAsync("Invalid credentials.");
        }
        catch (InvalidOperationException)
        {
            return await req.ForbiddenAsync("Account is locked or inactive.");
        }
    }
}
