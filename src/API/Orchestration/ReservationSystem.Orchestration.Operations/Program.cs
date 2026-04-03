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

        services.AddHttpClient("AncillaryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["AncillaryMs:BaseUrl"] ?? "https://reservation-system-microservice-ancillary-dkdfdjfba9fcbvfk.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["AncillaryMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"] ?? "https://reservation-system-db-microservice-order-cnc3fpdzfucbhudc.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["OrderMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["DeliveryMs:BaseUrl"] ?? "https://reservation-system-db-microservice-delivery-ehe2f4c3dybehwat.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["DeliveryMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"] ?? "https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["CustomerMs:HostKey"];
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
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<DeliveryServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetFlightStatusHandler>();
        services.AddScoped<ImportSsimHandler>();
        services.AddScoped<ImportSchedulesToInventoryHandler>();
        services.AddScoped<OciRetrieveHandler>();
        services.AddScoped<OciPaxHandler>();
    })
    .Build();

host.Run();
