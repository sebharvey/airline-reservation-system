// Author: Seb Harvey
// Description: Entry point and host configuration for the Template Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Template.TemplateApi.Application.CreatePerson;
using ReservationSystem.Template.TemplateApi.Application.CreateTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.DeletePerson;
using ReservationSystem.Template.TemplateApi.Application.DeleteTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.GetAllPersons;
using ReservationSystem.Template.TemplateApi.Application.GetAllTemplateItems;
using ReservationSystem.Template.TemplateApi.Application.GetExchangeRate;
using ReservationSystem.Template.TemplateApi.Application.GetPerson;
using ReservationSystem.Template.TemplateApi.Application.GetTemplateItem;
using ReservationSystem.Template.TemplateApi.Application.UpdatePerson;
using ReservationSystem.Template.TemplateApi.Domain.ExternalServices;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
using ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices;
using ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Health;
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

        // EF Core DbContext for [dbo].[Persons] — scoped lifetime (one per function invocation).
        // Connection string is shared with the existing Dapper SqlConnectionFactory.
        services.AddDbContext<PersonsDbContext>((provider, options) =>
        {
            var dbOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;
            options.UseSqlServer(dbOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });
        });
        services.AddScoped<IPersonRepository, EfPersonRepository>();

        // Named HttpClient for the currency exchange API — timeout is driven by options.
        services.AddHttpClient<ICurrencyExchangeClient, CurrencyExchangeClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ExchangeRateClientOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck(
            $"{nameof(SqlTemplateItemRepository)}.{nameof(ITemplateItemRepository.GetAllAsync)}",
            sp => ct => sp.GetRequiredService<ITemplateItemRepository>().GetAllAsync(ct));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<GetTemplateItemHandler>();
        services.AddScoped<GetAllTemplateItemsHandler>();
        services.AddScoped<CreateTemplateItemHandler>();
        services.AddScoped<DeleteTemplateItemHandler>();
        services.AddScoped<GetExchangeRateHandler>();

        // Person handlers
        services.AddScoped<GetPersonHandler>();
        services.AddScoped<GetAllPersonsHandler>();
        services.AddScoped<CreatePersonHandler>();
        services.AddScoped<UpdatePersonHandler>();
        services.AddScoped<DeletePersonHandler>();
    })
    .Build();

host.Run();
