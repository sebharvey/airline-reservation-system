namespace ReservationSystem.Microservices.DatabaseMcp.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly string _apiKey = config["ApiKey"]
        ?? throw new InvalidOperationException("ApiKey configuration is required");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("x-api-key", out var key) || key != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next(context);
    }
}
