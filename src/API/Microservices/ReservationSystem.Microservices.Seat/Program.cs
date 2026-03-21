// Description: Entry point and host configuration for the Seat Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Seat.Application.CreateAircraftType;
using ReservationSystem.Microservices.Seat.Application.CreateSeatmap;
using ReservationSystem.Microservices.Seat.Application.CreateSeatPricing;
using ReservationSystem.Microservices.Seat.Application.DeleteSeatPricing;
using ReservationSystem.Microservices.Seat.Application.GetAircraftType;
using ReservationSystem.Microservices.Seat.Application.GetAllAircraftTypes;
using ReservationSystem.Microservices.Seat.Application.GetAllSeatPricings;
using ReservationSystem.Microservices.Seat.Application.GetSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetSeatOffer;
using ReservationSystem.Microservices.Seat.Application.GetSeatOffers;
using ReservationSystem.Microservices.Seat.Application.GetSeatPricing;
using ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Microservices.Seat.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Health;
using Microsoft.EntityFrameworkCore;
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
        services.AddDbContext<SeatDbContext>((provider, options) =>
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
        services.AddScoped<IAircraftTypeRepository, EfAircraftTypeRepository>();
        services.AddScoped<ISeatmapRepository, EfSeatmapRepository>();
        services.AddScoped<ISeatPricingRepository, EfSeatPricingRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetSeatmapHandler>();
        services.AddScoped<GetSeatOffersHandler>();
        services.AddScoped<GetSeatOfferHandler>();
        services.AddScoped<GetAllAircraftTypesHandler>();
        services.AddScoped<CreateAircraftTypeHandler>();
        services.AddScoped<GetAircraftTypeHandler>();
        services.AddScoped<UpdateAircraftTypeHandler>();
        services.AddScoped<GetAllSeatPricingsHandler>();
        services.AddScoped<CreateSeatPricingHandler>();
        services.AddScoped<GetSeatPricingHandler>();
        services.AddScoped<UpdateSeatPricingHandler>();
        services.AddScoped<DeleteSeatPricingHandler>();
        services.AddScoped<CreateSeatmapHandler>();
        services.AddScoped<UpdateSeatmapHandler>();
    })
    .Build();

host.Run();
