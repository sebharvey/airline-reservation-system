// Description: Entry point and host configuration for the Delivery Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Delivery.Swagger;
using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Application.DeleteManifest;
using ReservationSystem.Microservices.Delivery.Application.GetDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;
using ReservationSystem.Microservices.Delivery.Application.GetManifest;
using ReservationSystem.Microservices.Delivery.Application.GetTicketsByBooking;
using ReservationSystem.Microservices.Delivery.Application.IssueTickets;
using ReservationSystem.Microservices.Delivery.Application.PatchManifest;
using ReservationSystem.Microservices.Delivery.Application.ReissueTickets;
using ReservationSystem.Microservices.Delivery.Application.UpdateFlightTimes;
using ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;
using ReservationSystem.Microservices.Delivery.Application.VoidDocument;
using ReservationSystem.Microservices.Delivery.Application.VoidTicket;
using ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;
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
        services.AddDbContext<DeliveryDbContext>((provider, options) =>
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
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<IManifestRepository, EfManifestRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetTicketsByBookingHandler>();
        services.AddScoped<IssueTicketsHandler>();
        services.AddScoped<VoidTicketHandler>();
        services.AddScoped<ReissueTicketsHandler>();
        services.AddScoped<CreateManifestHandler>();
        services.AddScoped<UpdateManifestSeatHandler>();
        services.AddScoped<PatchManifestHandler>();
        services.AddScoped<UpdateFlightTimesHandler>();
        services.AddScoped<DeleteManifestHandler>();
        services.AddScoped<GetManifestHandler>();
        services.AddScoped<CreateDocumentHandler>();
        services.AddScoped<GetDocumentHandler>();
        services.AddScoped<GetDocumentsByBookingHandler>();
        services.AddScoped<VoidDocumentHandler>();
        services.AddScoped<CreateBoardingCardsHandler>();
    })
    .Build();

host.Run();
