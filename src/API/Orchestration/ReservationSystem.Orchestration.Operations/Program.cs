// Author: Seb Harvey
// Description: Entry point and host configuration for the Operations Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Operations.Swagger;
using ReservationSystem.Orchestration.Operations.Application.GetFlightStatus;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
using ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;
using ReservationSystem.Orchestration.Operations.Application.OciRetrieve;
using ReservationSystem.Orchestration.Operations.Application.OciPax;
using ReservationSystem.Orchestration.Operations.Application.OciCheckIn;
using ReservationSystem.Orchestration.Operations.Application.HandleDelay;
using ReservationSystem.Orchestration.Operations.Application.HandleCancellation;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionChange;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionGetOrders;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionRebookOrder;
using ReservationSystem.Orchestration.Operations.Application.AdminCheckIn;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionTime;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Shared.Business.Middleware;

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
            client.BaseAddress = new Uri(context.Configuration["ScheduleMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("AncillaryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["AncillaryMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["DeliveryMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
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
        services.AddScoped<FareFamilyServiceClient>();
        services.AddScoped<BagServiceClient>();
        services.AddScoped<ProductServiceClient>();
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<DeliveryServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetFlightStatusHandler>();
        services.AddScoped<ImportSsimHandler>();
        services.AddScoped<ImportSchedulesToInventoryHandler>();
        services.AddScoped<AdminCheckInHandler>();
        services.AddScoped<OciRetrieveHandler>();
        services.AddScoped<OciPaxHandler>();
        services.AddScoped<OciCheckInHandler>();
        services.AddScoped<HandleDelayHandler>();
        services.AddScoped<HandleCancellationHandler>();
        services.AddScoped<AdminDisruptionCancelHandler>();
        services.AddScoped<AdminDisruptionChangeHandler>();
        services.AddScoped<AdminDisruptionTimeHandler>();
        services.AddScoped<AdminDisruptionGetOrdersHandler>();
        services.AddScoped<AdminDisruptionRebookOrderHandler>();
    })
    .Build();

host.Run();
