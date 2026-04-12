// Description: Entry point and host configuration for the Simulator Azure Functions timer trigger

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Simulator.Application.RunSimulator;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── HTTP clients ───────────────────────────────────────────────────────
        services.AddHttpClient("RetailApi", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["RetailApi:BaseUrl"]
                ?? "https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["RetailApi:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddScoped<IRetailApiClient, RetailApiClient>();

        // ── Application handlers ───────────────────────────────────────────────
        services.AddScoped<RunSimulatorHandler>();
    })
    .Build();

host.Run();
