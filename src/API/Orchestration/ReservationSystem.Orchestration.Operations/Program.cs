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
using ReservationSystem.Shared.Common.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<TerminalAuthenticationMiddleware>();
        worker.UseNewtonsoftJson();
    })
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
            client.BaseAddress = new Uri(context.Configuration["ScheduleMs:BaseUrl"] ?? "https://reservation-system-db-microservice-schedule-cvbebgdqgcbpeeb7.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["ScheduleMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"] ?? "https://reservation-system-db-microservice-offer-dnfdbebdezemaghp.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["OfferMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("SeatMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["SeatMs:BaseUrl"] ?? "https://reservation-system-db-microservice-seat-d3crfphwhqazcwgz.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["SeatMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<ScheduleServiceClient>();
        services.AddScoped<OfferServiceClient>();
        services.AddScoped<SeatServiceClient>();

        services.AddScoped<FareRuleServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<ImportSsimHandler>();
        services.AddScoped<ImportSchedulesToInventoryHandler>();
    })
    .Build();

host.Run();
