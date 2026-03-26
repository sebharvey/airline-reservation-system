// Description: Entry point and host configuration for the Payment Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Payment.Application.AuthorisePayment;
using ReservationSystem.Microservices.Payment.Application.InitialisePayment;
using ReservationSystem.Microservices.Payment.Application.RefundPayment;
using ReservationSystem.Microservices.Payment.Application.SettlePayment;
using ReservationSystem.Microservices.Payment.Application.VoidPayment;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Infrastructure.Persistence;
using ReservationSystem.Microservices.Payment.Swagger;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;

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

        // ── Configuration ──────────────────────────────────────────────────────
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddDbContext<PaymentDbContext>((provider, options) =>
        {
            var dbOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;
            options.UseSqlServer(dbOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });
        });
        services.AddHttpClient();
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<InitialisePaymentHandler>();
        services.AddScoped<AuthorisePaymentHandler>();
        services.AddScoped<SettlePaymentHandler>();
        services.AddScoped<RefundPaymentHandler>();
        services.AddScoped<VoidPaymentHandler>();
    })
    .Build();

host.Run();
