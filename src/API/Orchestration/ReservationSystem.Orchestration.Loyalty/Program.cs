// Author: Seb Harvey
// Description: Entry point and host configuration for the Loyalty Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Loyalty.Swagger;
using ReservationSystem.Orchestration.Loyalty.Application.EmailChangeRequest;
using ReservationSystem.Orchestration.Loyalty.Application.Login;
using ReservationSystem.Orchestration.Loyalty.Application.Logout;
using ReservationSystem.Orchestration.Loyalty.Application.PasswordReset;
using ReservationSystem.Orchestration.Loyalty.Application.PasswordResetRequest;
using ReservationSystem.Orchestration.Loyalty.Application.RefreshToken;
using ReservationSystem.Orchestration.Loyalty.Application.Register;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.GetPreferences;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using ReservationSystem.Orchestration.Loyalty.Application.UpdatePreferences;
using ReservationSystem.Orchestration.Loyalty.Application.DeleteAccount;
using ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;
using ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;
using ReservationSystem.Orchestration.Loyalty.Application.SearchCustomers;
using ReservationSystem.Orchestration.Loyalty.Application.TransferPoints;
using ReservationSystem.Orchestration.Loyalty.Application.VerifyEmailChange;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Shared.Business.Middleware;
using ReservationSystem.Orchestration.Loyalty.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<TerminalAuthenticationMiddleware>();
        worker.UseMiddleware<TokenVerificationMiddleware>();
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
        services.AddHttpClient("IdentityMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["IdentityMs:BaseUrl"]!);
            var hostKey = context.Configuration["IdentityMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"]!);
            var hostKey = context.Configuration["CustomerMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<IdentityServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<PasswordResetRequestHandler>();
        services.AddScoped<PasswordResetHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<GetProfileHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<GetPreferencesHandler>();
        services.AddScoped<UpdatePreferencesHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<GetTransactionsHandler>();
        services.AddScoped<AuthorisePointsHandler>();
        services.AddScoped<TransferPointsHandler>();
        services.AddScoped<EmailChangeRequestHandler>();
        services.AddScoped<VerifyEmailChangeHandler>();
        services.AddScoped<SearchCustomersHandler>();
    })
    .Build();

host.Run();
