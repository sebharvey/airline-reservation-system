using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Middleware;

/// <summary>
/// Best-effort middleware that enriches the FunctionContext with loyalty identity when
/// a valid Bearer token is present. Requests without a token pass through unchanged,
/// preserving existing unauthenticated Retail flows. Admin functions are skipped
/// as they are handled by TerminalAuthenticationMiddleware.
///
/// On a valid token the resolved <c>UserAccountId</c>, <c>UserEmail</c>, and
/// (when a matching Customer record exists) <c>LoyaltyNumber</c> are stored in
/// <see cref="FunctionContext.Items"/> for use by downstream handlers.
/// </summary>
public sealed class TokenVerificationMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (functionName.StartsWith("Admin", StringComparison.OrdinalIgnoreCase))
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

        if (!requestData.Headers.TryGetValues("Authorization", out var authValues))
        {
            await next(context);
            return;
        }

        var authHeader = authValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        var identityClient = context.InstanceServices.GetRequiredService<IdentityServiceClient>();
        var verifyResult = await identityClient.VerifyTokenAsync(token);

        if (verifyResult is null || !verifyResult.Valid)
        {
            await next(context);
            return;
        }

        context.Items["UserAccountId"] = verifyResult.UserAccountId;
        context.Items["UserEmail"] = verifyResult.Email;

        var customerClient = context.InstanceServices.GetRequiredService<CustomerServiceClient>();
        var loyaltyNumber = await customerClient.GetLoyaltyNumberByIdentityIdAsync(verifyResult.UserAccountId);
        if (!string.IsNullOrEmpty(loyaltyNumber))
            context.Items["LoyaltyNumber"] = loyaltyNumber;

        await next(context);
    }
}
