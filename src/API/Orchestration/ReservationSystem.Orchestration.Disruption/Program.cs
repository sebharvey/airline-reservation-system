// Author: Seb Harvey
// Description: Entry point and host configuration for the Disruption Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Disruption.Swagger;
using ReservationSystem.Orchestration.Disruption.Application.HandleDelay;
using ReservationSystem.Orchestration.Disruption.Application.HandleCancellation;
using ReservationSystem.Orchestration.Disruption.Infrastructure.ExternalServices;

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
        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = context.Configuration["OfferMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = context.Configuration["OrderMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = context.Configuration["DeliveryMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = context.Configuration["CustomerMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = context.Configuration["PaymentMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<OfferServiceClient>();
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<DeliveryServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<HandleDelayHandler>();
        services.AddScoped<HandleCancellationHandler>();
    })
    .Build();

host.Run();
