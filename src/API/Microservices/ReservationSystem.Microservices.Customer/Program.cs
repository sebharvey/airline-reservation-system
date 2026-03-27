// Description: Entry point and host configuration for the Customer Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Customer.Application.AddPoints;
using ReservationSystem.Microservices.Customer.Application.AuthorisePoints;
using ReservationSystem.Microservices.Customer.Application.CreateCustomer;
using ReservationSystem.Microservices.Customer.Application.DeleteCustomer;
using ReservationSystem.Microservices.Customer.Application.GetCustomer;
using ReservationSystem.Microservices.Customer.Application.GetTransactions;
using ReservationSystem.Microservices.Customer.Application.ReinstatePoints;
using ReservationSystem.Microservices.Customer.Application.ReversePoints;
using ReservationSystem.Microservices.Customer.Application.SettlePoints;
using ReservationSystem.Microservices.Customer.Application.SearchCustomers;
using ReservationSystem.Microservices.Customer.Application.TransferPoints;
using ReservationSystem.Microservices.Customer.Application.UpdateCustomer;
using ReservationSystem.Microservices.Customer.Application.GetPreferences;
using ReservationSystem.Microservices.Customer.Application.UpdatePreferences;
using ReservationSystem.Microservices.Customer.Domain.Repositories;
using ReservationSystem.Microservices.Customer.Infrastructure.Persistence;
using ReservationSystem.Microservices.Customer.Swagger;
using ReservationSystem.Shared.Common.Health;
using Microsoft.EntityFrameworkCore;
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
        services.AddDbContext<CustomerDbContext>((provider, options) =>
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
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<ILoyaltyTransactionRepository, EfLoyaltyTransactionRepository>();
        services.AddScoped<ICustomerPreferencesRepository, EfCustomerPreferencesRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<DeleteCustomerHandler>();
        services.AddScoped<GetTransactionsHandler>();
        services.AddScoped<AuthorisePointsHandler>();
        services.AddScoped<SettlePointsHandler>();
        services.AddScoped<ReversePointsHandler>();
        services.AddScoped<ReinstatePointsHandler>();
        services.AddScoped<AddPointsHandler>();
        services.AddScoped<SearchCustomersHandler>();
        services.AddScoped<TransferPointsHandler>();
        services.AddScoped<GetCustomerByIdentityHandler>();
        services.AddScoped<GetPreferencesHandler>();
        services.AddScoped<UpdatePreferencesHandler>();
    })
    .Build();

host.Run();
