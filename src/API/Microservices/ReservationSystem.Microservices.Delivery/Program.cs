// Description: Entry point and host configuration for the Delivery Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Application.GetDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;
using ReservationSystem.Microservices.Delivery.Application.GetManifest;
using ReservationSystem.Microservices.Delivery.Application.GetManifestTickets;
using ReservationSystem.Microservices.Delivery.Application.GetTicket;
using ReservationSystem.Microservices.Delivery.Application.IssueTicket;
using ReservationSystem.Microservices.Delivery.Application.ReissueTicket;
using ReservationSystem.Microservices.Delivery.Application.UpdateManifest;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;
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
        services.AddScoped<IManifestRepository, EfManifestRepository>();
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<CreateManifestHandler>();
        services.AddScoped<GetManifestHandler>();
        services.AddScoped<UpdateManifestHandler>();
        services.AddScoped<IssueTicketHandler>();
        services.AddScoped<ReissueTicketHandler>();
        services.AddScoped<GetTicketHandler>();
        services.AddScoped<GetManifestTicketsHandler>();
        services.AddScoped<CreateDocumentHandler>();
        services.AddScoped<GetDocumentHandler>();
        services.AddScoped<GetDocumentsByBookingHandler>();
    })
    .Build();

host.Run();
