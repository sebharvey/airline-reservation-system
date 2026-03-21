using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Seat.Swagger;
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
using ReservationSystem.Microservices.Seat.Application.DeleteAircraftType;
using ReservationSystem.Microservices.Seat.Application.DeleteSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetAllSeatmaps;
using ReservationSystem.Microservices.Seat.Application.GetSeatmapById;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Microservices.Seat.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
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
        services.AddScoped<IAircraftTypeRepository, SqlAircraftTypeRepository>();
        services.AddScoped<ISeatmapRepository, SqlSeatmapRepository>();
        services.AddScoped<ISeatPricingRepository, SqlSeatPricingRepository>();

        // ── Health check ────────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ───────────────────────────────────────
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
        services.AddScoped<DeleteAircraftTypeHandler>();
        services.AddScoped<GetAllSeatmapsHandler>();
        services.AddScoped<GetSeatmapByIdHandler>();
        services.AddScoped<CreateSeatmapHandler>();
        services.AddScoped<UpdateSeatmapHandler>();
        services.AddScoped<DeleteSeatmapHandler>();
    })
    .Build();

host.Run();
