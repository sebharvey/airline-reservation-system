// Author: Seb Harvey
// Description: Entry point and host configuration for the Operations Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Operations.Swagger;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
using ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ── OpenAPI ────────────────────────────────────────────────────────────
        services.AddSingleton<IOpenApiConfigurationOptions, OpenApiConfigurationOptions>();

        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Named HttpClients for downstream microservices ─────────────────────
        services.AddHttpClient("ScheduleMs", client =>
        {
            client.BaseAddress = context.Configuration["ScheduleMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = context.Configuration["OfferMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<ScheduleServiceClient>();
        services.AddScoped<OfferServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<ImportSsimHandler>();
        services.AddScoped<ImportSchedulesToInventoryHandler>();
    })
    .Build();

host.Run();
