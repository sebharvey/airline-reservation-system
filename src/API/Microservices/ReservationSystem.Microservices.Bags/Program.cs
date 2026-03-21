using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Bags.Swagger;
using ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.CreateBagPricing;
using ReservationSystem.Microservices.Bags.Application.DeleteBagPolicy;
using ReservationSystem.Microservices.Bags.Application.DeleteBagPricing;
using ReservationSystem.Microservices.Bags.Application.GetAllBagPolicies;
using ReservationSystem.Microservices.Bags.Application.GetAllBagPricings;
using ReservationSystem.Microservices.Bags.Application.GetBagPolicy;
using ReservationSystem.Microservices.Bags.Application.GetBagPricing;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;
using ReservationSystem.Microservices.Bags.Domain.Repositories;
using ReservationSystem.Microservices.Bags.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ── OpenAPI ────────────────────────────────────────────────────────────
        services.AddSingleton<IOpenApiConfigurationOptions, OpenApiConfigurationOptions>();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<IBagPolicyRepository, SqlBagPolicyRepository>();
        services.AddScoped<IBagPricingRepository, SqlBagPricingRepository>();

        // ── Health check ────────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ───────────────────────────────────────
        services.AddScoped<GetBagPolicyHandler>();
        services.AddScoped<GetAllBagPoliciesHandler>();
        services.AddScoped<CreateBagPolicyHandler>();
        services.AddScoped<UpdateBagPolicyHandler>();
        services.AddScoped<DeleteBagPolicyHandler>();
        services.AddScoped<GetBagPricingHandler>();
        services.AddScoped<GetAllBagPricingsHandler>();
        services.AddScoped<CreateBagPricingHandler>();
        services.AddScoped<UpdateBagPricingHandler>();
        services.AddScoped<DeleteBagPricingHandler>();
    })
    .Build();

host.Run();
