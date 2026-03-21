using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Offer.Swagger;
using ReservationSystem.Microservices.Offer.Application.CancelInventory;
using ReservationSystem.Microservices.Offer.Application.CreateFare;
using ReservationSystem.Microservices.Offer.Application.CreateFlight;
using ReservationSystem.Microservices.Offer.Application.GetSeatAvailability;
using ReservationSystem.Microservices.Offer.Application.GetStoredOffer;
using ReservationSystem.Microservices.Offer.Application.HoldInventory;
using ReservationSystem.Microservices.Offer.Application.ReleaseInventory;
using ReservationSystem.Microservices.Offer.Application.ReserveSeat;
using ReservationSystem.Microservices.Offer.Application.SearchOffers;
using ReservationSystem.Microservices.Offer.Application.SellInventory;
using ReservationSystem.Microservices.Offer.Application.UpdateSeatStatus;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Infrastructure.Persistence;
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

        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<IOfferRepository, SqlOfferRepository>();

        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        services.AddScoped<CreateFlightHandler>();
        services.AddScoped<CreateFareHandler>();
        services.AddScoped<SearchOffersHandler>();
        services.AddScoped<GetStoredOfferHandler>();
        services.AddScoped<HoldInventoryHandler>();
        services.AddScoped<SellInventoryHandler>();
        services.AddScoped<ReleaseInventoryHandler>();
        services.AddScoped<CancelInventoryHandler>();
        services.AddScoped<GetSeatAvailabilityHandler>();
        services.AddScoped<ReserveSeatHandler>();
        services.AddScoped<UpdateSeatStatusHandler>();
    })
    .Build();

host.Run();
