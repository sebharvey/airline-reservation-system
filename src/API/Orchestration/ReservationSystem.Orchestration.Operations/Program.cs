// Author: Seb Harvey
// Description: Entry point and host configuration for the Operations Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Operations.Swagger;
using ReservationSystem.Orchestration.Operations.Application.CreateSchedule;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
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
            client.BaseAddress = new Uri(context.Configuration["ScheduleMs:BaseUrl"] ?? "https://localhost:7071/");
        });

        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"] ?? "https://localhost:7072/");
        });

        services.AddHttpClient("IdentityMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["IdentityMs:BaseUrl"] ?? "https://localhost:7073/");
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<ScheduleServiceClient>();
        services.AddScoped<OfferServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<CreateScheduleHandler>();
        services.AddScoped<ImportSsimHandler>();
    })
    .Build();

host.Run();
