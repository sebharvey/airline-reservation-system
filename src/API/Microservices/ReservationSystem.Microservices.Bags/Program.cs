using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Configuration ──────────────────────────────────────────────────────
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddDbContext<BagsDbContext>((provider, options) =>
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

        services.AddScoped<IBagPolicyRepository, EfBagPolicyRepository>();
        services.AddScoped<IBagPricingRepository, EfBagPricingRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.CompletedTask);

        // ── Application use-case handlers ──────────────────────────────────────
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
