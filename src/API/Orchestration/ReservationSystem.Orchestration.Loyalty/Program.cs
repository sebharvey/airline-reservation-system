// Author: Seb Harvey
// Description: Entry point and host configuration for the Loyalty Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Loyalty.Application.Login;
using ReservationSystem.Orchestration.Loyalty.Application.Register;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Named HttpClients for downstream microservices ─────────────────────
        services.AddHttpClient("IdentityMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["IdentityMs:BaseUrl"] ?? "https://localhost:7071/");
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"] ?? "https://localhost:7072/");
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<IdentityServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<LoginHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<GetProfileHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<AuthorisePointsHandler>();
    })
    .Build();

host.Run();
