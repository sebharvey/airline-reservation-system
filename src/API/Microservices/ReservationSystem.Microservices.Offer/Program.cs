// Author: Seb Harvey
// Description: Entry point and host configuration for the Offer Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Offer.Application.UseCases.CreateOffer;
using ReservationSystem.Microservices.Offer.Application.UseCases.DeleteOffer;
using ReservationSystem.Microservices.Offer.Application.UseCases.GetAllOffers;
using ReservationSystem.Microservices.Offer.Application.UseCases.GetOffer;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Configuration ──────────────────────────────────────────────────────
        // Bind the "Database" section from host.json / environment variables.
        // In Azure, set Application Settings: Database__ConnectionString, etc.
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<IOfferRepository, SqlOfferRepository>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetOfferHandler>();
        services.AddScoped<GetAllOffersHandler>();
        services.AddScoped<CreateOfferHandler>();
        services.AddScoped<DeleteOfferHandler>();
    })
    .Build();

host.Run();
