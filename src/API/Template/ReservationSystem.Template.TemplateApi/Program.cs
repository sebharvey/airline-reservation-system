// Author: Seb Harvey
// Description: Entry point and host configuration for the Template Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Template.TemplateApi.Application.UseCases.CreateTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.DeleteTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetAllTemplateItems;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetExchangeRate;
using ReservationSystem.Template.TemplateApi.Application.UseCases.GetTemplateItem;
using ReservationSystem.Template.TemplateApi.Domain.ExternalServices;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
using ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices;
using ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;
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

        // Bind the "ExchangeRateApi" section from host.json / environment variables.
        // In Azure, set Application Settings: ExchangeRateApi__BaseUrl, ExchangeRateApi__ApiKey, etc.
        services.Configure<ExchangeRateClientOptions>(
            context.Configuration.GetSection(ExchangeRateClientOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<ITemplateItemRepository, SqlTemplateItemRepository>();

        // Named HttpClient for the currency exchange API — timeout is driven by options.
        services.AddHttpClient<ICurrencyExchangeClient, CurrencyExchangeClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ExchangeRateClientOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetTemplateItemHandler>();
        services.AddScoped<GetAllTemplateItemsHandler>();
        services.AddScoped<CreateTemplateItemHandler>();
        services.AddScoped<DeleteTemplateItemHandler>();
        services.AddScoped<GetExchangeRateHandler>();
    })
    .Build();

host.Run();
