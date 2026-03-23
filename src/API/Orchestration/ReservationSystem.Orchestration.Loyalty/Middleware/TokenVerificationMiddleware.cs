using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Middleware;

/// <summary>
/// Azure Functions isolated worker middleware that enforces JWT authentication on
/// all protected routes. Public routes (login, refresh, logout, register) bypass
/// verification. All others require a valid Bearer token, which is verified by
/// calling the Identity microservice POST /v1/auth/verify.
///
/// On success the resolved <c>UserAccountId</c> and <c>Email</c> are stored in
/// <see cref="FunctionContext.Items"/> for use by downstream handlers.
/// </summary>
public sealed class TokenVerificationMiddleware : IFunctionsWorkerMiddleware
{
    // Function names (matched against FunctionDefinition.Name) that are public.
    private static readonly HashSet<string> PublicFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Login",
        "RefreshToken",
        "Logout",
        "PasswordResetRequest",
        "PasswordReset",
        "RegisterMember",
        "HealthCheck",
        "VerifyEmail",
    };

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (PublicFunctions.Contains(functionName))
        {
            await next(context);
            return;
        }

        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData is null)
        {
            // Non-HTTP trigger — skip auth (e.g. timer triggers).
            await next(context);
            return;
        }

        // Extract Bearer token from Authorization header.
        if (!requestData.Headers.TryGetValues("Authorization", out var authValues))
        {
            await RejectAsync(context, requestData);
            return;
        }

        var authHeader = authValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, requestData);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        // Delegate verification to Identity microservice.
        var identityClient = context.InstanceServices.GetRequiredService<IdentityServiceClient>();
        var verifyResult = await identityClient.VerifyTokenAsync(token);

        if (verifyResult is null || !verifyResult.Valid)
        {
            await RejectAsync(context, requestData);
            return;
        }

        // Expose verified identity to downstream handlers via FunctionContext.Items.
        context.Items["UserAccountId"] = verifyResult.UserAccountId;
        context.Items["UserEmail"] = verifyResult.Email;

        await next(context);
    }

    private static async Task RejectAsync(FunctionContext context, HttpRequestData requestData)
    {
        var response = requestData.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new { error = "Unauthorized." }));

        context.GetInvocationResult().Value = response;
    }
}
