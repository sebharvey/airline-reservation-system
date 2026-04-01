using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Exceptions.Application.GetExceptions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Configuration ──────────────────────────────────────────────────────
        services.Configure<AppInsightsOptions>(
            context.Configuration.GetSection(AppInsightsOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddSingleton(_ => new LogsQueryClient(new DefaultAzureCredential()));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetExceptionsHandler>();
    })
    .Build();

host.Run();
