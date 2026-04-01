// Description: Entry point and host configuration for the Order Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Order.Swagger;
using ReservationSystem.Microservices.Order.Application.CancelOrder;
using ReservationSystem.Microservices.Order.Application.ChangeOrder;
using ReservationSystem.Microservices.Order.Application.ConfirmOrder;
using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.DeleteExpiredBaskets;
using ReservationSystem.Microservices.Order.Application.DeleteExpiredDraftOrders;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Application.ExpireBasket;
using ReservationSystem.Microservices.Order.Application.GetBasket;
using ReservationSystem.Microservices.Order.Application.GetOrder;
using ReservationSystem.Microservices.Order.Application.RebookOrder;
using ReservationSystem.Microservices.Order.Application.UpdateBasketBags;
using ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;
using ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSsrs;
using ReservationSystem.Microservices.Order.Application.UpdateOrderBags;
using ReservationSystem.Microservices.Order.Application.UpdateOrderETickets;
using ReservationSystem.Microservices.Order.Application.UpdateOrderPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;
using ReservationSystem.Microservices.Order.Application.GetSsrOptions;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using ReservationSystem.Microservices.Order.Infrastructure.Persistence;
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
        services.AddDbContext<OrderDbContext>((provider, options) =>
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
        services.AddScoped<IBasketRepository, EfBasketRepository>();
        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<ISsrCatalogueRepository, EfSsrCatalogueRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<DeleteExpiredBasketsHandler>();
        services.AddScoped<DeleteExpiredDraftOrdersHandler>();
        services.AddScoped<CreateBasketHandler>();
        services.AddScoped<GetBasketHandler>();
        services.AddScoped<UpdateBasketFlightsHandler>();
        services.AddScoped<UpdateBasketSeatsHandler>();
        services.AddScoped<UpdateBasketBagsHandler>();
        services.AddScoped<UpdateBasketPassengersHandler>();
        services.AddScoped<UpdateBasketSsrsHandler>();
        services.AddScoped<ExpireBasketHandler>();
        services.AddScoped<CreateOrderHandler>();
        services.AddScoped<ConfirmOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<UpdateOrderPassengersHandler>();
        services.AddScoped<UpdateOrderSeatsHandler>();
        services.AddScoped<UpdateOrderBagsHandler>();
        services.AddScoped<UpdateOrderSsrsHandler>();
        services.AddScoped<UpdateOrderETicketsHandler>();
        services.AddScoped<CancelOrderHandler>();
        services.AddScoped<ChangeOrderHandler>();
        services.AddScoped<RebookOrderHandler>();
        services.AddScoped<GetSsrOptionsHandler>();
    })
    .Build();

host.Run();
