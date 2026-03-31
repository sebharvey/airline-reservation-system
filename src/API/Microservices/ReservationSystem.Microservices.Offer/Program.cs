using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Offer.Swagger;
using ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;
using ReservationSystem.Microservices.Offer.Application.DeleteExpiredFlightInventory;
using ReservationSystem.Microservices.Offer.Application.DeleteExpiredStoredOffers;
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
using ReservationSystem.Microservices.Offer.Application.CreateFareRule;
using ReservationSystem.Microservices.Offer.Application.UpdateFareRule;
using ReservationSystem.Microservices.Offer.Application.DeleteFareRule;
using ReservationSystem.Microservices.Offer.Application.GetFareRule;
using ReservationSystem.Microservices.Offer.Application.SearchFareRules;
using ReservationSystem.Microservices.Offer.Application.GetFlightInventory;
using ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;
using ReservationSystem.Microservices.Offer.Application.GetFlightByInventoryId;
using ReservationSystem.Microservices.Offer.Application.RollingInventoryImport;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Infrastructure.ExternalServices;
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

        // ── Named HttpClients for rolling inventory import (timer trigger) ────────
        services.AddHttpClient("ScheduleMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["ScheduleMs:BaseUrl"] ?? "https://reservation-system-db-microservice-schedule-cvbebgdqgcbpeeb7.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["ScheduleMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("SeatMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["SeatMs:BaseUrl"] ?? "https://reservation-system-db-microservice-seat-d3crfphwhqazcwgz.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["SeatMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddScoped<ScheduleServiceClient>();
        services.AddScoped<SeatServiceClient>();
        services.AddScoped<RollingInventoryImportHandler>();

        services.AddScoped<DeleteExpiredFlightInventoryHandler>();
        services.AddScoped<DeleteExpiredStoredOffersHandler>();
        services.AddScoped<CreateFlightHandler>();
        services.AddScoped<CreateFareHandler>();
        services.AddScoped<BatchCreateFlightsHandler>();
        services.AddScoped<SearchOffersHandler>();
        services.AddScoped<GetStoredOfferHandler>();
        services.AddScoped<HoldInventoryHandler>();
        services.AddScoped<SellInventoryHandler>();
        services.AddScoped<ReleaseInventoryHandler>();
        services.AddScoped<CancelInventoryHandler>();
        services.AddScoped<GetSeatAvailabilityHandler>();
        services.AddScoped<ReserveSeatHandler>();
        services.AddScoped<UpdateSeatStatusHandler>();

        services.AddScoped<CreateFareRuleHandler>();
        services.AddScoped<UpdateFareRuleHandler>();
        services.AddScoped<DeleteFareRuleHandler>();
        services.AddScoped<GetFareRuleHandler>();
        services.AddScoped<SearchFareRulesHandler>();
        services.AddScoped<GetFlightInventoryHandler>();
        services.AddScoped<GetFlightInventoryByDateHandler>();
        services.AddScoped<GetFlightByInventoryIdHandler>();
    })
    .Build();

host.Run();
