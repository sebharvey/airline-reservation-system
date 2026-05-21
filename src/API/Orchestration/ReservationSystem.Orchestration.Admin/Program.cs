// Author: Seb Harvey
// Description: Entry point and host configuration for the Admin Orchestration Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Business.Middleware;
using ReservationSystem.Orchestration.Admin.Swagger;
using ReservationSystem.Orchestration.Admin.Application.Login;
using ReservationSystem.Orchestration.Admin.Application.GetPaymentsByDate;
using ReservationSystem.Orchestration.Admin.Application.GetPayment;
using ReservationSystem.Orchestration.Admin.Application.GetPaymentEvents;
using ReservationSystem.Orchestration.Admin.Application.GetSsrOptions;
using ReservationSystem.Orchestration.Admin.Application.CreateSsrOption;
using ReservationSystem.Orchestration.Admin.Application.UpdateSsrOption;
using ReservationSystem.Orchestration.Admin.Application.DeactivateSsrOption;
using ReservationSystem.Orchestration.Admin.Application.GetAllUsers;
using ReservationSystem.Orchestration.Admin.Application.GetUser;
using ReservationSystem.Orchestration.Admin.Application.CreateUser;
using ReservationSystem.Orchestration.Admin.Application.UpdateUser;
using ReservationSystem.Orchestration.Admin.Application.SetUserStatus;
using ReservationSystem.Orchestration.Admin.Application.UnlockUser;
using ReservationSystem.Orchestration.Admin.Application.ResetPassword;
using ReservationSystem.Orchestration.Admin.Application.DeleteUser;
using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseNewtonsoftJson();
        worker.UseMiddleware<TerminalAuthenticationMiddleware>();
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
        services.AddHttpClient("UserMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["UserMs:BaseUrl"]!);
            var hostKey = context.Configuration["Microservice:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"]!);
            var hostKey = context.Configuration["Microservice:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["PaymentMs:BaseUrl"]!);
            var hostKey = context.Configuration["Microservice:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<UserServiceClient>();
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<PaymentServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<LoginHandler>();

        // Payment
        services.AddScoped<GetPaymentsByDateHandler>();
        services.AddScoped<GetPaymentHandler>();
        services.AddScoped<GetPaymentEventsHandler>();

        // SSR
        services.AddScoped<GetSsrOptionsHandler>();
        services.AddScoped<CreateSsrOptionHandler>();
        services.AddScoped<UpdateSsrOptionHandler>();
        services.AddScoped<DeactivateSsrOptionHandler>();

        // User
        services.AddScoped<GetAllUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<SetUserStatusHandler>();
        services.AddScoped<UnlockUserHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<DeleteUserHandler>();
    })
    .Build();

host.Run();
