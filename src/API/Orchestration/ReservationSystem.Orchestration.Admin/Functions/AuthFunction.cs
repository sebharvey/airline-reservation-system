using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Admin.Application.Login;
using ReservationSystem.Orchestration.Admin.Models.Requests;
using ReservationSystem.Orchestration.Admin.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Orchestration.Admin.Functions;

/// <summary>
/// HTTP-triggered functions for staff authentication.
/// Orchestrates calls to the User microservice.
/// Login is a public route — no Bearer token required.
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
    // POST /v1/auth/login
    // -------------------------------------------------------------------------

    [Function("Login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" }, Summary = "Authenticate a staff member and return a JWT access token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginRequest), Required = true, Description = "Login credentials: username and password")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Description = "OK – returns accessToken, userId, expiresAt, tokenType")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – missing fields")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized – invalid credentials")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Forbidden, Description = "Forbidden – account locked or inactive")]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<LoginRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Username) || string.IsNullOrWhiteSpace(request.Password))
            return await req.BadRequestAsync("The fields 'username' and 'password' are required.");

        try
        {
            var command = new LoginCommand(request.Username, request.Password);
            var result = await _loginHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 401)
        {
            return await req.UnauthorizedAsync("Invalid credentials.");
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 403)
        {
            return await req.ForbiddenAsync("Account is locked or inactive.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during staff login");
            return await req.InternalServerErrorAsync();
        }
    }
}
