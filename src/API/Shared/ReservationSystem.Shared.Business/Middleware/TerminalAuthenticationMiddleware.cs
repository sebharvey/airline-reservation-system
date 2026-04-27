using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Shared.Common.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ReservationSystem.Shared.Business.Middleware;

/// <summary>
/// Azure Functions middleware that validates staff JWT tokens (issued by the User
/// microservice) on admin-only routes. Staff tokens carry a <c>role</c> claim of
/// "User" and are signed with HMAC-SHA256 using a shared secret.
///
/// This middleware only applies to functions whose name starts with "Admin". All
/// other functions fall through to the existing token verification middleware.
/// </summary>
public sealed class TerminalAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private const string RequiredRole = "User";

    private readonly TokenValidationParameters? _tokenValidation;
    private readonly ILogger<TerminalAuthenticationMiddleware> _logger;

    public TerminalAuthenticationMiddleware(IConfiguration configuration, ILogger<TerminalAuthenticationMiddleware> logger)
    {
        _logger = logger;

        var secret = configuration["UserMs:JwtSecret"];

        if (!string.IsNullOrEmpty(secret))
        {
            var keyBytes = Convert.FromBase64String(secret);
            _tokenValidation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["UserMs:JwtIssuer"] ?? "apex-air-user",
                ValidateAudience = true,
                ValidAudience = configuration["UserMs:JwtAudience"] ?? "apex-air-reservation",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ClockSkew = TimeSpan.FromMinutes(2),
            };
        }
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (!functionName.StartsWith("Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData is null)
        {
            await next(context);
            return;
        }

        if (_tokenValidation is null)
        {
            _logger.LogError("Staff JWT validation is not configured (UserMs:JwtSecret is missing)");
            context.GetInvocationResult().Value = await requestData.InternalServerErrorAsync();
            return;
        }

        if (!requestData.Headers.TryGetValues("Authorization", out var authValues))
        {
            context.GetInvocationResult().Value = await requestData.UnauthorizedAsync("Unauthorized.");
            return;
        }

        var authHeader = authValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.GetInvocationResult().Value = await requestData.UnauthorizedAsync("Unauthorized.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _tokenValidation, out _);

            var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
                         ?? principal.FindFirst("role")?.Value;

            if (!string.Equals(roleClaim, RequiredRole, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Staff token missing required role claim '{Role}'", RequiredRole);
                context.GetInvocationResult().Value =
                    await requestData.ForbiddenAsync("Insufficient permissions.");
                return;
            }

            context.Items["StaffUserId"] = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
            context.Items["StaffUsername"] = principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? string.Empty;
            context.Items["StaffEmail"] = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug(ex, "Staff JWT validation failed");
            context.GetInvocationResult().Value = await requestData.UnauthorizedAsync("Unauthorized.");
            return;
        }

        await next(context);
    }
}
