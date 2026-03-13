// Author: Seb Harvey
// Description: Entry point and host configuration for the Template Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Template.TemplateApi.Application.UseCases.CreateTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.DeleteTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetAllTemplateItems;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetTemplateItem;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
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
        services.AddScoped<ITemplateItemRepository, SqlTemplateItemRepository>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetTemplateItemHandler>();
        services.AddScoped<GetAllTemplateItemsHandler>();
        services.AddScoped<CreateTemplateItemHandler>();
        services.AddScoped<DeleteTemplateItemHandler>();
    })
    .Build();

host.Run();
