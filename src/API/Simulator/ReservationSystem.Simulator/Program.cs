// Description: Entry point and host configuration for the Simulator Azure Functions timer trigger

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Simulator.Application.CheckInSimulator;
using ReservationSystem.Simulator.Application.RunSimulator;
using ReservationSystem.Simulator.Application.UpdateFlightOperationalData;
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
            client.BaseAddress = new Uri(context.Configuration["RetailApi:BaseUrl"]!);
            var hostKey = context.Configuration["RetailApi:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("AdminApi", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["AdminApi:BaseUrl"]!);
        });

        services.AddHttpClient("OperationsApi", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OperationsApi:BaseUrl"]!);
        });

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddScoped<IRetailApiClient, RetailApiClient>();
        services.AddScoped<IFlightUpdateClient, FlightUpdateClient>();

        // ── Application handlers ───────────────────────────────────────────────
        services.AddScoped<RunSimulatorHandler>();
        services.AddScoped<UpdateFlightOperationalDataHandler>();
        services.AddScoped<CheckInSimulatorHandler>();
    })
    .Build();

host.Run();
